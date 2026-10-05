# Kaleido Source Architecture

This document describes the architecture of the `src/` projects in detail. It covers project dependencies, internal structure, and the key design decisions within each project.

For the top-level repository model, see [`../ARCHITECTURE.md`](../ARCHITECTURE.md).

---

## Terminology

Canonical vocabulary — type names, namespaces, and docs must follow it:

| Term | Meaning |
|---|---|
| **Service** | A deployed Kaleido host — `Kaleido:ServiceName`, route prefix `/{service}` |
| **Processor** | The unit a service hosts that owns and executes Steps — the runtime/DI identity (`Kaleido.Processor` namespace, `IProcessorRuntime`, `ProcessorContext`, `IProcessorContextStore`, `AddProcessor`/`MapProcessor`, `kaleido.processor.*` telemetry) |
| **Process** | One executing workflow instance — the wire/transport domain (`/processes` routes, `ProcessId`, `ProcessExecutionState`, `ProcessStateResponse`, `ExecuteProcessRequest`, process events) |
| **Step** | A unit of work inside a Processor (`[ProcessStep]` — annotates the work, not the executor) |
| **Context / View** | Queryable-side: a queryable context and its named views (`Queryable` is the feature namespace) |
| **Delegated** | A view forwarded to a remote source — uniform `Delegated*` prefix (`IDelegatedQueryViewSource`, `DelegatedQueryViewRegistry`, `DelegatedQueryViewEngine`) |
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
- `KaleidoCorrelationContextAccessor` — scoped accessor for ambient correlation
- `TypeDescriber` — CLR type → `DataTypeDescriptor` projection and `IsSupportedType` check
- `ConstraintMapper` — `ValidationAttribute` → `ConstraintContract` projection
- `KaleidoCorrelationContext` — shared ambient identity
- `IEventPublisher` / `NullEventPublisher` — infrastructure-agnostic event seam

**`Kaleido.Queryable`**
- Registration: `QueryableServiceCollectionExtensions`, `QueryableBuilder`
- Runtime: `QueryableService` (dispatch), `QueryContextEngine`, `QueryContextExecutor`
- Extension point: `IQueryContextExecutor<TView>` is public — consumers on async-capable providers (e.g. EF Core) should register their own implementation so `CountAsync`/`ToListAsync` call provider-native async operators instead of the default `IAsyncEnumerable`/sync fallback.
- Planning: `QueryRequestCompiler`, `QueryRequestValidator`
- Registries: `IQueryContextRegistry`, `IQueryViewRegistry`, `IDelegatedQueryViewRegistry`
- Observability: `QueryableObservability`

**`Kaleido.Process`**
- Registration: `ProcessorServiceCollectionExtensions`
- Runtime: `ExecutionProcessor`
- Planning: `StepCandidateBuilder`, `StepCandidateValidator`, `StepCandidateConsistencyChecker`, `StepCandidatePlanner`
- State: `IProcessorContextStore`, `ProcessorContextStore`, `ProcessorContext`
- Registries: `ProcessorStepRegistry`, `ProcessorRegistry`
- Observability: `ProcessorObservability`

### Extension points

Public seams consumers are expected to implement or replace:

| Seam | Register via | Notes |
|------|--------------|-------|
| `IProcessorContextStore` | `UseSqliteProcessorContextStore(...)` or your own `services.AddScoped` after `AddKaleido()` | Production deployments implement against their own durable store; SQLite provider is a reference impl |
| `IEventPublisher` | `services.AddSingleton` before `AddKaleido()` | Default is no-op `NullEventPublisher`; replace for real event delivery |
| `IQueryContextExecutor<TView>` | `services.AddScoped<IQueryContextExecutor<TView>, ...>` | Provider-native async execution (e.g. EF Core `CountAsync`/`ToListAsync`) instead of sync fallback |
| Observability provider | `AddOpenTelemetry()` (Kaleido.Observability.OpenTelemetry) or custom `AddKaleidoInstrumentation()` calls | Core stays provider-agnostic on BCL `ActivitySource`/`Meter` |
| Delegated query views | implement `IDelegatedQueryViewSource<TDelegateContext,TView>` on a query view type | Federates view execution to a downstream delegate context |

### Key design invariants
- The core project has no transport dependencies.
- `KaleidoServiceOptions.Assemblies` records assemblies; recording does not scan them for capabilities. Scanning happens during the `AddQueryable()` / `AddProcessor()` calls that `AddKaleido()` invokes internally.
- `QueryableService` dispatch order (delegated → local → direct) is a published semantic and must not change casually.
- `ProcessorContext` is current resumable state only, not an audit log.
- `IKaleidoBuilder` is intentionally minimal.
- `CountAsync` runs only when `Page` is explicitly provided AND the returned page is full (`items.Count == page.Size`). When `Page` is absent or the page is partial, `TotalCount = items.Count` — the caller received all results.
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
- `GET /{prefix}/queryable/{context}/{metadataRoute}` — per-context metadata
- `POST /{prefix}/queryable/{context}/{queryRoute}` — direct context query
- `POST /{prefix}/queryable/{context}/{view}/{queryRoute}` — view query

**Process endpoints** (`ProcessorEndpointRouteBuilderExtensions`)
- `GET /{prefix}/processes/steps/{step}/metadata` — per-step metadata
- `POST /{prefix}/processes/execute` — multi-step execute
- `GET /{prefix}/processes/{processId}` — process state
- `POST /{prefix}/processes/steps/{step}` — per-step execute

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
- Reference types: `ProcessStepInfo`, `ProcessStepSummary`, `ProcessorRegistryResponse`, `ProcessStepResponse`

**Queryable contracts**
- Request: `QueryApiRequest`, `QueryApiRequest<TParameters>` — accepts `QueryApiBody` (transport shape)
- Transport body: `QueryApiBody`, `QueryApiFilterNode`, `QueryApiFilterCondition`, `QueryApiFilterGroup`, `QueryApiSort`, `QueryApiPage` — string enums, raw `JsonElement` filter values
- Response: `QueryableRecordResponse` (carries `ServiceName`, `RegistryUrl`), `QueryableFieldMetadata`, `QueryableQueryParameter`, `QueryableQueryProperty`, `QueryErrorResponse`
- `QueryApiBodyExtensions.ToApiBody()` — converts runtime `QueryBody` → `QueryApiBody` for core callers forwarding over HTTP (e.g. delegated view sources)
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
- `IKaleidoProcessorClient` — typed interface: `GetRegistryAsync`, `GetStepMetadataAsync`, `GetProcessStateAsync`, `ExecuteAsync`, `ExecuteStepAsync`, `ExecuteStepAsync<TStep, TResult>`
- `KaleidoProcessorClient` — concrete implementation; lazily fetches and caches the remote registry per client instance
- `KaleidoHttpClientException` — thrown on non-success responses and on registry lookup failures
- `KaleidoClientServiceCollectionExtensions` — internal `AddProcessorClient(...)` registration used by `AddHttpClients`

**Queryable client**
- `IKaleidoQueryableClient` — typed interface: `GetRegistryAsync`, `GetContextMetadataAsync`, `QueryViewAsync`, `QueryContextAsync`
- `KaleidoQueryableClient` — concrete implementation; lazily fetches and caches the remote registry per client instance
- `KaleidoQueryableClientException` — thrown on non-success responses and on registry lookup failures
- `KaleidoQueryableClientServiceCollectionExtensions` — internal `AddQueryableClient(...)` registration used by `AddHttpClients`

### Key design invariants
- Both clients lazily fetch and cache the remote registry for the lifetime of the client instance.
- Both clients automatically forward Kaleido correlation headers on outbound requests.
- `AddHttpClients()` (in `KaleidoHttpClientsServiceCollectionExtensions`) registers both a Process and a Queryable client per entry in `Kaleido:Clients`, reading base URLs from `Kaleido:Clients:<Name>:BaseUrl` with a `Kaleido:BaseUrl` fallback.
- The client name is lowercased to derive the route prefix, matching the remote server's `Kaleido:ServiceName`.

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
- `SqliteProcessorContextStore` — implements `IProcessorContextStore` using SQLite via EF Core
- `SqliteProcessorContextStoreServiceCollectionExtensions` — `UseSqliteProcessorContextStore(connectionString)` extension; replaces the default in-memory store

### Key design invariants
- Calling `UseSqliteProcessorContextStore(...)` replaces the in-memory `IProcessorContextStore` registered by `AddProcessor(...)`.
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
`IEventPublisher` is registered by `AddKaleido()` as a no-op `NullEventPublisher` by default. Replace it before calling `AddKaleido()` to install real event infrastructure.

Publishing is fire-and-forget in both Process and Queryable: the runtime persists process state first, then calls `IEventPublisher.PublishAsync` without awaiting it. An exception from the call is logged at Warning and never fails the request or rolls back state; delivery, ordering and retries are owned by the consumer's publisher. A transactional outbox is deliberately out of framework scope — consumers needing exactly-once delivery implement their own outbox (e.g. Debezium/WAL tailing against their durable store) since topologies differ per deployment.

### Discoverability
Metadata is derived from CLR types at startup:
- `DataTypeMapper` converts CLR property types to `DataTypeDescriptor` values
- `ConstraintMapper` converts `ValidationAttribute` usage to `ConstraintContract` values
- Both are used by the Queryable and Process runtimes when building discovery metadata for HTTP endpoints
