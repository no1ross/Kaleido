# Kaleido Source Architecture

This document describes the architecture of the `src/` projects in detail. It covers project dependencies, internal structure, and the key design decisions within each project.

For the top-level repository model, see [`../ARCHITECTURE.md`](../ARCHITECTURE.md).

---

## Terminology

Canonical vocabulary — type names, namespaces, and docs must follow it:

| Term | Meaning |
|---|---|
| **Service** | A deployed Kaleido host — `Kaleido:ServiceName`, route prefix `/{service}` |
| **Processor** | The unit a service hosts that owns and executes Steps — the runtime/DI identity (`Kaleido.Processor` namespace, `IProcessorRuntime`, `ProcessorContext`, `IKaleidoContextStore`, `AddProcessor`/`MapProcessor`, `kaleido.processor.*` telemetry) |
| **Process** | One executing workflow instance — the wire/transport domain (`/processes` routes, `ProcessId`, `ProcessExecutionState`, `ProcessStateResponse`, `ExecuteProcessRequest`, process events) |
| **Step** | A unit of work inside a Processor — identified by `IProcessStep`, described by `[ProcessStep]`, named by its type name (it marks the work, not the executor) |
| **Source / View / Query context** | Queryable-side (`Queryable` is the feature namespace). Names are type names. <br>- **Query source** (`IQuerySource<T>`, described by `[QuerySource]`): the published capability. <br>- **Query view** (`IQueryViewSource<TSource, …>`, `[QueryView]`): projects a source. <br>- **Query context** (`IQueryContext`): the record that describes how a source is queried. |
| **Delegated** | A query source that fulfils queries downstream (a facade): `IDelegatedQuerySource`, `DelegatedQuerySourceEngine`. Not visible to consumers |
| **Registry / Snapshot** | Discovery envelope (`AggregatedRegistryResponse`); cached under `kaleido:{serviceName}` in `IRegistrySnapshotStore` |

Rule of thumb: if a name refers to *who runs the work* (runtime, registries, planner, executor, stores, telemetry identity) it's `Processor*`; if it refers to *one executing instance* (request/response/state, routes, events) it's `Process*`. Transport namespaces (`Kaleido.Http.Processor`, `Kaleido.Http.Client.Processor`, `Kaleido.Http.Abstractions.Processor`) organize the processor-facing API surface even though the wire resource is `processes`.

---

## Project dependency graph

```
Kaleido.Http.Client ──────────────────────────────────┐
                                                       ↓
Kaleido.Http ──────────────────────────────────► Kaleido.Http.Abstractions
                                                       ↑
Kaleido ◄──────────────────────────────────────────────┘

Kaleido.Provider.SQLite ──► Kaleido
Kaleido.Observability.OpenTelemetry ──► Kaleido
```

- `Kaleido` has no Kaleido project dependencies — it is the foundation.
- `Kaleido.Http` depends on `Kaleido.Http.Abstractions` (which depends on `Kaleido`).
- `Kaleido.Http.Client` depends on `Kaleido` and `Kaleido.Http.Abstractions`.
- `Kaleido.Observability.OpenTelemetry` depends on `Kaleido` only.
- `Kaleido.Provider.SQLite` depends on `Kaleido` only.

---

## 1. Kaleido (core)

### Internal structure

The core project is organized into two main namespaces:

**`Kaleido` (bootstrap and shared)**
- `KaleidoServiceCollectionExtensions` — `AddKaleido()` (assemblies via `KaleidoServiceOptions.Assemblies`)
- `IKaleidoBuilder` / `KaleidoBuilder` — minimal shared builder
- `IKaleidoCorrelationContextAccessor` — scoped accessor for ambient correlation (a pre-existing registration wins)
- `TypeDescriber` (internal) — CLR type → `DataTypeDescriptor` projection and `IsSupportedType` check
- `ConstraintMapper` (internal) — `ValidationAttribute` → `ConstraintContract` projection
- `KaleidoCorrelationContext` — shared ambient identity
- `IEventPublisher` — infrastructure-agnostic event seam; `AddKaleido()` registers an internal no-op publisher (with a Warning log) until the host calls `AddEventPublisher<T>()`

**`Kaleido.Queryable`**
- Registration: `QueryableServiceCollectionExtensions` (internal, invoked by `AddKaleido()`)
- Runtime: `QueryableService` (dispatch by queried type), `QueryContextEngine`, `DelegatedQuerySourceEngine`, `QueryContextExecutor`
- Extension point: `IQueryContextExecutor<TView>` is public — consumers on async-capable providers (e.g. EF Core) should register their own implementation so `CountAsync`/`ToListAsync` call provider-native async operators instead of the default `IAsyncEnumerable`/sync fallback.
- Planning: `QueryRequestCompiler`, `QueryRequestValidator`
- Registries: `IQuerySourceRegistry`, `IQueryViewRegistry`, `IQueryableRegistry` (published projection)
- Observability: `QueryableObservability`

**`Kaleido.Processor`**
- Registration: `ProcessorServiceCollectionExtensions` (internal, invoked by `AddKaleido()`)
- Runtime: `IProcessorRuntime`, `ProcessorExecutor` (the step execution loop)
- Planning: `StepCandidateBuilder`, `StepCandidateValidator`, `StepCandidateConsistencyChecker`, `StepCandidatePlanner`, `StepCandidateNextStepChecker` (only next steps run; answers checked against the pending information request)
- State: `IKaleidoContextStore` (in `Kaleido.Storage`), the internal in-memory default, `ProcessorContext`
- Registries: `IProcessorStepRegistry`, `IProcessorRegistry`
- Observability: `ProcessorObservability`

### Extension points

Public seams consumers are expected to implement or replace:

| Seam | Register via | Notes |
|------|--------------|-------|
| `IKaleidoContextStore` | `UseSqliteKaleidoContextStore(...)` or your own `services.AddScoped` after `AddKaleido()` | Production deployments implement against their own durable store; SQLite provider is a reference impl |
| `IEventPublisher` | `AddEventPublisher<TPublisher>()` on the builder | Default is an internal no-op publisher (nothing is written to the outbox; a Warning says events are discarded); replace for real event delivery - the relay hands it every event at least once |
| `IQueryContextExecutor<TView>` | `services.AddSingleton(typeof(IQueryContextExecutor<>), typeof(MyExecutor<>))` (the default is added with `TryAdd`) | Provider-native execution (e.g. EF Core `CountAsync`/`ToListAsync`): the default counts an async query by reading every matching row; see `docs/PATTERNS.md` |
| Observability provider | `AddOpenTelemetry()` (Kaleido.Observability.OpenTelemetry) or custom `AddKaleidoInstrumentation()` calls | Core stays provider-agnostic on BCL `ActivitySource`/`Meter` |
| Delegated query sources | implement `IDelegatedQuerySource<TQueryContext, TResult[, TParameters]>` and add `[QuerySource]` | A facade: translates the consumer's query (validated against `TQueryContext`), calls a downstream source or view, and maps to `TResult` |

### Key design invariants
- The core project has no transport dependencies.
- `KaleidoServiceOptions.Assemblies` records assemblies; recording does not scan them for capabilities. Scanning happens during the `AddQueryable()` / `AddProcessor()` calls that `AddKaleido()` invokes internally.
- Queryable discovery is interface-only and a type is exactly one capability; `QueryableService` dispatches by the queried type. How a source is fulfilled (local or delegated) is never published.
- `ProcessorContext` is current resumable state only, not an audit log.
- `IKaleidoBuilder` is intentionally minimal.
- `TotalCount` is always the number of matching results. A pageable capability always pages (its default page when the request names none). `CountAsync` runs only when the page cannot tell the total: the page is full, or empty past the start. A partial page ends the results (`TotalCount = offset + items.Count`); an unpageable capability returns every result (`TotalCount = items.Count`).
- Core runtime types (`QueryBody`, `QueryFilterNode`, `FilterOperator`, `SortDirection`, `LogicalOperator`) are transport-agnostic — they use CLR enums and `object?` values. The HTTP transport converts `QueryApiBody` (string enums, `JsonElement` values) to `QueryBody` via `QueryBodyResolver` before handing to core.

---

## 2. Kaleido.Http

### Internal structure

**Registration and pipeline**
- `KaleidoHttpServiceCollectionExtensions` — `AddHttp()` builder extension; registers routing, `IHttpContextAccessor`, the middleware pipeline, and HTTP execution services
- `KaleidoStartupFilter` — registers middlewares via `IStartupFilter` in the correct pipeline order
- `ExceptionMiddleware` — outermost middleware; maps exceptions to JSON error responses
- `ObservabilityMiddleware` — reads inbound correlation headers, populates `IKaleidoCorrelationContextAccessor`, tags the `Activity`, echoes correlation headers on the response
- `HttpCorrelationContextReader` — reads and sanitizes inbound HTTP headers into `KaleidoCorrelationContext`

**Transport services**
- `ProcessExecutionService` — translates HTTP execute requests into runtime `ProcessRequest` values and writes the resolved `ProcessId` into the response headers
- `ProcessStateService` — reads durable process state and maps it to the HTTP response contract

**Transport conversion**
- `QueryBodyResolver` — converts `QueryApiBody` (HTTP transport shape, string enums + `JsonElement` values) to `QueryBody` (runtime shape, typed CLR values + enums) before handing to core. Uses `QueryableFieldDescriptor.FieldType` for value resolution.
- `KaleidoJsonEndpointFilter` — `IEndpointFilter` applied to all Kaleido route groups; wraps `IValueHttpResult` responses in `Results.Json(..., KaleidoJsonOptions.Options)` so enums serialize as strings without touching global `JsonOptions`.
- `KaleidoJsonOptions.Options` — shared `JsonSerializerOptions` in `Kaleido.Http.Abstractions`, used by both server filter and client.

**Queryable endpoints** (`QueryableEndpointRouteBuilderExtensions`)
- `POST /{prefix}/queryable/{source}/query` — direct query of a source (every local or delegated source)
- `POST /{prefix}/queryable/{source}/{view}/query` — view query

**Process endpoints** (`ProcessorEndpointRouteBuilderExtensions`)
- `POST /{prefix}/processes/execute` — multi-step execute
- `GET /{prefix}/processes/{processId}` — process state
- `POST /{prefix}/processes/steps/{step}` — per-step execute
- Step metadata is published by the registry endpoint (no per-step metadata endpoint)

**Registry endpoint** (`RegistryEndpointRouteBuilderExtensions` — internal; mapped by `MapKaleidoHttp`)
- `GET /{prefix}/registry` — unified discovery envelope (`AggregatedRegistryResponse`: `Processes`, `Queryables`, `ClientErrors`)
- Leaf mode returns this service's local registrations; `MapKaleidoHttp(o => o.AggregateRegistry = true)` also fans out to every `AddHttpClients()` client — one `GET /{svc}/registry` fetch per client, shared via `KaleidoRemoteRegistry`
- Freshness: `GeneratedAt`, `Revision` (SHA-256 of the filtered payload), `IsPartial`; `ETag`/`Cache-Control` headers, `If-None-Match` → 304; `?strict` → 502 when partial (body still included); `?refresh` forces rebuild, throttled by `KaleidoHttpMapOptions.RegistryRefreshCooldown`
- Snapshots live in `IRegistrySnapshotStore` (in-memory default; distributed impl optional) under canonical key `kaleido:{ServiceName}` — a shared store lets consumers skip the HTTP call entirely on a fresh hit; only clean snapshots are committed, so degraded states are re-probed per request
- Always returns HTTP 200 unless `?strict` is requested; unreachable downstream clients populate `ClientErrors`

**URL generation**
- `ProcessContractUrls` / `ProcessRoutePaths` (`Kaleido.Http.Abstractions/Processor/ProcessRoutes.cs`)
- `QueryableContractUrls` (`Kaleido.Http.Abstractions/Queryable/QueryableRoutes.cs`)

### Key design invariants
- Endpoints adapt contracts and publish routes. They do not reimplement runtime planning or business execution.
- The route prefix is derived from `KaleidoServiceOptions.ServiceName` (bound from `Kaleido:ServiceName` configuration).
- Registry endpoint returns 200 by default. Partial responses are signalled through `ClientErrors`/`IsPartial`; `?strict` opts into a 502 when partial.
- `ConfigureHttpJsonOptions` is never called — Kaleido JSON options are scoped to Kaleido endpoints via `KaleidoJsonEndpointFilter`. Consumer endpoints retain their own serialization behavior.

---

## 3. Kaleido.Http.Abstractions

### Internal structure

**Process contracts**
- Request: `ExecuteProcessRequest`, `ExecuteStepRequest<TStep>`
- Response: `ProcessExecutionResponse`, `ProcessExecutionStepResponse`, `StepExecutionResponse`, `StepExecutionResponse<TResult>`, `ProcessStateResponse`
- Reference types: `ProcessRequiredStep`, `ProcessStepSummary`, `ProcessorRegistryResponse`, `ProcessStepResponse`

**Queryable contracts**
- Request: `QueryApiRequest`, `QueryApiRequest<TParameters>` — accepts `QueryApiBody` (transport shape)
- Transport body: `QueryApiBody`, `QueryApiFilterNode`, `QueryApiFilterCondition`, `QueryApiFilterGroup`, `QueryApiSort`, `QueryApiPage` — string enums, raw `JsonElement` filter values
- Response: `QueryableSourceResponse` (carries `ServiceName`, `RegistryUrl`), `QueryableFieldMetadata`, `QueryableViewResponse`; errors use the core `KaleidoErrorResponse`
- `QueryApiBodyExtensions.ToApiBody()` — converts runtime `QueryBody` → `QueryApiBody` for core callers forwarding over HTTP (e.g. delegated query sources)
- `KaleidoJsonOptions.Options` — shared `JsonSerializerOptions` with `JsonStringEnumConverter` for all Kaleido HTTP serialization

### Key design invariants
- This project defines the published HTTP contract surface shared by server (`Kaleido.Http`) and client (`Kaleido.Http.Client`).
- Treat it as a public API boundary. Prefer additive changes.
- `QueryApiBody` and its sub-types are the wire shape — string enums, `JsonElement` values. Core `QueryBody`/`QueryFilterNode`/`FilterOperator` are never serialized to HTTP directly.
- `KaleidoJsonOptions.Options` is the single `JsonSerializerOptions` for all Kaleido HTTP serialization — server responses and client read/write. All `ReadFromJsonAsync`/`JsonContent.Create` calls in Kaleido code must pass it explicitly.

---

## 4. Kaleido.Http.Client

### Internal structure

**Process client**
- `IKaleidoProcessorClient` — typed interface: `GetRegistryAsync`, `InvalidateRegistryAsync`, `GetProcessStateAsync`, `ExecuteAsync`, `ExecuteStepAsync<TStep>`, `ExecuteStepAsync<TStep, TResult>` (the step name is `typeof(TStep).Name`)
- `KaleidoProcessorClient` — concrete implementation; lazily fetches and caches the remote registry per client instance
- `KaleidoHttpClientException` — thrown on non-success responses and on registry lookup failures
- `KaleidoClientServiceCollectionExtensions` — internal `AddProcessorClient(...)` registration used by `AddHttpClients`

**Queryable client**
- `IKaleidoQueryableClient` — interface: `GetRegistryAsync`, `InvalidateRegistryAsync`, `QueryViewAsync(source, view, …)`, `QuerySourceAsync(source, …)` (with and without parameters). Sources and views are addressed by their published names (strings): the caller cannot reference the remote types
- `KaleidoQueryableClient` — concrete implementation; lazily fetches and caches the remote registry per client instance
- Non-success responses and registry lookup failures throw `KaleidoHttpClientException`, as for the Process client
- `KaleidoClientServiceCollectionExtensions` — internal `AddQueryableClient(...)` registration used by `AddHttpClients`

### Key design invariants
- Both clients share one cached registry per downstream (`KaleidoRemoteRegistry`, keyed `kaleido:{routePrefix}`), revalidated every `RegistryTtl` (default 30 s) with `If-None-Match` and dropped when a capability call answers 404.
- Both clients automatically forward Kaleido correlation headers on outbound requests.
- `AddHttpClients()` (in `KaleidoHttpClientsServiceCollectionExtensions`) registers both a Process and a Queryable client per entry in `Kaleido:Clients`, reading base URLs from `Kaleido:Clients:<Name>:BaseUrl` with a `Kaleido:BaseUrl` fallback.
- The route prefix defaults to the client name lowercased (`RoutePrefix` overrides it) and must equal the remote server's `Kaleido:ServiceName`.

---

## 5. Kaleido.Observability.OpenTelemetry

### Internal structure
- `KaleidoObservabilityOpenTelemetryExtensions` — `AddOpenTelemetry()` on `IKaleidoBuilder` (logging + tracing + metrics + OTLP) and `AddKaleidoInstrumentation()` on `TracerProviderBuilder`/`MeterProviderBuilder`
- Instrumentation itself stays in `Kaleido` (BCL `ActivitySource`/`Meter`); this project only wires the OTel SDK

### Key design invariants
- Core is observability-provider-agnostic; this is one possible `Kaleido.Observability.<Technology>` provider.

---

## 6. Kaleido.Provider.SQLite

### Internal structure
- `SqliteKaleidoContextStore` — implements `IKaleidoContextStore` using SQLite via EF Core
- `SqliteKaleidoContextStoreServiceCollectionExtensions` — `UseSqliteKaleidoContextStore(connectionString)` extension; replaces the default in-memory store

### Key design invariants
- Calling `UseSqliteKaleidoContextStore(...)` replaces the in-memory `IKaleidoContextStore` registered by `AddKaleido()`: process state, idempotent results and the event outbox share one database.
- The store must correctly implement state reconciliation so that existing saved contexts remain valid when the step registry changes.

---

## Cross-cutting concerns

### Correlation identity
`KaleidoCorrelationContext` flows through all layers:
- initialized from HTTP headers by `HttpCorrelationContextReader` / `ObservabilityMiddleware` (in `Kaleido.Http`)
- accessed via the scoped `IKaleidoCorrelationContextAccessor` (registered by `AddKaleido()` in `Kaleido`)
- forwarded by HTTP clients as outbound headers (in `Kaleido.Http.Client`)
- included as tags on observability activities (in `Kaleido`)

#### Correlation context invariants

These are framework invariants — code that violates them produces split traces:

- **Exactly one RequestId per HTTP request** — `ObservabilityMiddleware` echoes the inbound
  `X-Kaleido-Request-Id` if present, else generates one. Handlers and downstream calls must
  propagate the ambient `RequestId`, never mint a second one.
- **Echo-or-generate** — every Kaleido response echoes the end-to-end pair (`RequestId`,
  `ProcessId`). An absent `RequestId` is generated, not silently dropped. Per-hop headers
  are never echoed.
- **End-to-end vs per-hop** — `RequestId`/`ProcessId` are forwarded unchanged on every hop.
  `X-Kaleido-Calling-Processor`/`-Calling-Step` are set by the caller only when the call is
  made from inside a step, and never forwarded. The processor instance id is never on the
  wire. See [`../docs/CORRELATION.md`](../docs/CORRELATION.md).
- **Outbound propagation is automatic** — `KaleidoProcessorClient`/`KaleidoQueryableClient`
  stamp headers via `CorrelationHeaderStamper`. Do not set `X-Kaleido-*` headers manually
  on outbound requests.
- **Scoped identity, ambient read** — resolve `IKaleidoCorrelationContextAccessor` (scoped);
  treat the context as read-only after request init. Mutating it mid-request splits the
  trace. The one sanctioned write is `ProcessorStepInvoker` seeding a step handler's child
  scope (request context + `ProcessId` + `ExecutingStepName`).
- **`X-Kaleido-Process-Id` is a handle, not identity** — it resumes a durable process; it
  is honored unconditionally and is not part of the identity-trust gate
  (`TrustCorrelationIdentity`).

### Observability
Both Queryable and Process publish observability through activity sources and meters:
- Queryable: `Kaleido.Queryable` activity source and meter
- Process: `Kaleido.Processor` activity source and meter (names defined in `ProcessorTelemetry`)

### Event publishing
`IEventPublisher` is registered by `AddKaleido()` as a no-op publisher by default. Replace it with `AddEventPublisher<TPublisher>()` to install real event infrastructure.

Events are handed to `IEventPublisher` **at least once, never on the request path** ([ADR 0012](../docs/decisions/0012-events-at-least-once-through-the-store.md)):
- **Outbox in the store.** The runtime writes every event to the outbox of `IKaleidoContextStore`. Events that describe a save go in the same transaction as the state: `ProcessCreated` (a new process is saved right away) and `StepCompleted`. `PlanBuilt`, `ExecutionCompleted` and `QueryExecuted` are enqueued right after; an enqueue failure is logged and counted, never fails the request. Without a registered publisher nothing is written.
- **Relay.** `KaleidoEventRelay`, a hosted background service registered by `AddKaleido()` (one per instance), claims a batch (`Kaleido:Events:BatchSize`), publishes each entry through a Polly pipeline (timeout + circuit breaker, no Polly retries; `Kaleido:Events:Resilience`) and removes it on success. A failure is retried with exponential backoff (the durable schedule lives in the outbox, so it survives restarts); after `MaxRetryAttempts` the entry is parked. A write wakes the local relay at once; otherwise it polls, backing off to 10 s while the outbox is empty. Claims are leases, so an entry is in one instance's hands at a time; when an instance stops, its leases expire and another takes over.
- **Privacy.** Payloads are complete (the audit trail); before the outbox write, the registered `IKaleidoEventRedactor`s run and properties marked with a `DataClassificationAttribute` are redacted with the host's `IRedactorProvider` (`AddRedaction`), or erased when none is registered (`KaleidoEventPayloadWriter`, ADR 0013). A crash between a successful publish and its removal publishes the event again - consumers drop duplicates by `IKaleidoEvent.EventId`.

### Discoverability
Metadata is derived from CLR types at startup:
- `TypeDescriber` (internal) converts CLR property types to `DataTypeDescriptor` values
- `ConstraintMapper` (internal) converts `ValidationAttribute` usage to `ConstraintContract` values
- Both are used by the Queryable and Process runtimes when building discovery metadata for HTTP endpoints
