# Kaleido Source Architecture

This document describes the architecture of the `src/` projects in detail. It covers project dependencies, internal structure, and the key design decisions within each project.

For the top-level repository model, see [`../ARCHITECTURE.md`](../ARCHITECTURE.md).

---

## Terminology

Canonical vocabulary — type names, namespaces, and docs must follow it:

| Term | Meaning |
| --- | --- |
| **Service** | A deployed Kaleido host — `Kaleido:ServiceName` and `ServiceVersion`, public route `/{service}/{version}` |
| **Processor** | The unit a service hosts that owns and executes Steps — the runtime/DI identity (`Kaleido.Processor` namespace, `IProcessorRuntime`, `ProcessorContext`, `IKaleidoContextStore`, the internal `AddProcessor`/`MapProcessor` that `AddKaleido()`/`MapKaleidoHttp()` call, `kaleido.processor.*` telemetry) |
| **Process** | One executing workflow instance — the wire/transport domain (`/processes` routes, `ProcessId`, `ProcessExecutionState`, `ProcessStateResponse`, `ExecuteProcessRequest`, process events) |
| **Step** | A unit of work inside a Processor — identified by `IProcessStep`, described by `[ProcessStep]`, named by its type name (it marks the work, not the executor) |
| **Source / View / Query context** | Queryable-side (`Queryable` namespace); names are type names. A **query source** (`IQuerySource<T>`, `[QuerySource]`) is the published capability; a **query view** (`IQueryViewSource<TSource, …>`, `[QueryView]`) projects it; a **query context** (`IQueryContext`) describes how it is queried. |
| **Delegated** | A query source that fulfils queries downstream (a facade): `IDelegatedQuerySource`, `DelegatedQuerySourceEngine`. Not visible to consumers |
| **Central registry** | Service-version registration and discovery (`KaleidoRegistry`); each participant hydrates a full in-memory copy from the control registry |

Rule of thumb: if a name refers to *who runs the work* (runtime, registries, planner, executor, stores, telemetry identity) it's `Processor*`; if it refers to *one executing instance* (request/response/state, routes, events) it's `Process*`. `Kaleido.Platform.Processor`, `Kaleido.Http.Server.Processor` and `Kaleido.Http.Client.Processor` separate contracts and adapters even though the HTTP resource is `processes`.

---

## Project dependency graph

```text
Kaleido ─► Kaleido.Platform ─► Kaleido.Server, Kaleido.Client, Kaleido.Http
Kaleido.Server + Kaleido.Http ─► Kaleido.Http.Server
Kaleido.Client + Kaleido.Http ─► Kaleido.Http.Client
Kaleido.Server ─► Kaleido.Registry
Kaleido.Registry + Kaleido.Http.Server ─► Kaleido.Registry.Http
Kaleido.Http.Server + Kaleido.Http.Client ─► Kaleido.Mcp
Kaleido + Kaleido.Registry ─► Kaleido.Provider.SQLite
Kaleido ─► Kaleido.Observability.OpenTelemetry
```

Arrows mean "is referenced by". The gRPC and registry-gRPC projects mirror these boundaries but
are non-packable placeholders; no protocol adapter depends on another. Each project's `csproj`
records the direct `ProjectReference` edges.

---

## 1. Kaleido (core)

### Core internal structure

The core project is organized around three main areas:

#### Bootstrap and shared (`Kaleido`)

- `KaleidoServiceCollectionExtensions` — `AddKaleido()` (assemblies via `KaleidoServiceOptions.Assemblies`)
- `IKaleidoBuilder` / `KaleidoBuilder` — minimal shared builder
- `IKaleidoCorrelationContextAccessor` — scoped accessor for ambient correlation (a pre-existing registration wins)
- `TypeDescriber` (internal) — CLR type → `DataTypeDescriptor` projection and `IsSupportedType` check
- `ConstraintMapper` (internal) — `ValidationAttribute` → `ConstraintContract` projection
- `KaleidoCorrelationContext` — shared ambient identity
- `IEventPublisher` — infrastructure-agnostic event seam; registered only by `AddOutboxRelay<TPublisher>()`, on the host that runs the relay (ADR 0036)

#### Queryable runtime (`Kaleido.Queryable`)

- Registration: `QueryableServiceCollectionExtensions` (internal, invoked by `AddKaleido()`)
- Runtime: `QueryableService` (dispatch by queried type), `QueryContextEngine`, `DelegatedQuerySourceEngine`, `QueryContextExecutor`
- Extension point: `IQueryContextExecutor<TView>` is public — consumers on async-capable providers (e.g. EF Core, MongoDB) should register their own implementation so `CountAsync`/`ToListAsync` call provider-native async operators instead of the default `IAsyncEnumerable`/sync fallback.
- Planning: `QueryRequestCompiler`, `QueryRequestValidator`
- Registries: `IQuerySourceRegistry`, `IQueryViewRegistry`, `IQueryableRegistry` (published projection)
- Observability: `QueryableObservability`

#### Process runtime (`Kaleido.Processor`)

- Registration: `ProcessorServiceCollectionExtensions` (internal, invoked by `AddKaleido()`)
- Runtime: `IProcessorRuntime` (internal, called by the HTTP transport), `ProcessorExecutor` (the step execution loop)
- Planning: `StepCandidateBuilder`, `StepCandidateValidator`, `StepCandidateConsistencyChecker`, `StepCandidatePlanner`, `StepCandidateNextStepChecker` (only next steps run; answers checked against the pending information request)
- State: `IKaleidoContextStore` (in `Kaleido.Storage`, next to `IKaleidoOutboxStore`, `IKaleidoTransactions` and `IKaleidoIdempotencyStore`; every record scoped by the service and its version, `ProcessKey`), the internal in-memory defaults, `ProcessorContext`; `ProcessorTransition` (internal) runs every process-state save: begin, save the state, add its event to the outbox, commit, committed (ADR 0023, ADR 0036)
- Registries: `IProcessorStepRegistry`, `IProcessorRegistry` (both internal, shared with `Kaleido.Http.Server`)
- Observability: `ProcessorObservability`

### Extension points

Public seams consumers are expected to implement or replace:

| Seam | Register via | Notes |
| ------ | -------------- | ------- |
| `IKaleidoContextStore`, `IKaleidoIdempotencyStore`, `IKaleidoTransactions` | `AddContextStore<T>()`, `AddIdempotencyStore<T>()`, `AddTransactions<T>()` (or `AddSqliteContextStore`/`AddSqliteIdempotencyStore`) - one implementation each | Production deployments implement against their own durable store; SQLite provider is a reference impl |
| `IKaleidoOutboxStore` | one per service (`AddOutboxStore<T>()`/`AddSqliteOutboxStore`): every event it writes, tagged with the writer; several services may share its database | Atomic with the state only on the database `IKaleidoTransactions` covers (ADR 0023, ADR 0036) |
| `IEventPublisher` | `AddOutboxRelay<TPublisher>()` on the host that publishes (one per outbox database) | No default: services only write; the relay hands every event in its database to it at least once (ADR 0036) |
| `IQueryContextExecutor<TView>` | `services.AddSingleton(typeof(IQueryContextExecutor<>), typeof(MyExecutor<>))` (the default is added with `TryAdd`) | Provider-native execution (e.g. EF Core `CountAsync`/`ToListAsync`): the default counts an async query by reading every matching row; see `docs/PATTERNS.md` |
| Observability provider | `AddOpenTelemetry()` (Kaleido.Observability.OpenTelemetry) or custom `AddKaleidoInstrumentation()` calls | Core stays provider-agnostic on BCL `ActivitySource`/`Meter` |
| Delegated query sources | implement `IDelegatedQuerySource<TQueryContext, TResult[, TParameters]>` and add `[QuerySource]` | A facade: translates the consumer's query (validated against `TQueryContext`), calls a downstream source or view, and maps to `TResult` |

### Core design invariants

- The core project has no transport dependencies.
- `KaleidoServiceOptions.Assemblies` records assemblies; recording does not scan them for capabilities. Scanning happens during the `AddQueryable()` / `AddProcessor()` calls that `AddKaleido()` invokes internally.
- Queryable discovery is interface-only and a type is exactly one capability; `QueryableService` dispatches by the queried type. How a source is fulfilled (local or delegated) is never published.
- `ProcessorContext` is current resumable state only, not an audit log.
- `IKaleidoBuilder` is intentionally minimal.
- `TotalCount` is always the number of matching results. A pageable capability always pages (its default page when the request names none). `CountAsync` runs only when the page cannot tell the total: the page is full, or empty past the start. A partial page ends the results (`TotalCount = offset + items.Count`); an unpageable capability returns every result (`TotalCount = items.Count`).
- Core runtime types (`QueryBody`, `QueryFilterNode`, `FilterOperator`, `SortDirection`, `LogicalOperator`) are transport-agnostic — they use CLR enums and `object?` values. The HTTP transport converts `QueryApiBody` (string enums, `JsonElement` values) to `QueryBody` via `QueryBodyResolver` before handing to core.

---

## 2. Kaleido.Server and Kaleido.Http.Server

### Server and HTTP endpoint structure

#### Registration and pipeline

- `Kaleido.Server` owns caller/authorization, correlation, endpoint filters, error handling and the filtered in-host registry view. Its filters on each Kaleido route group are `KaleidoExceptionEndpointFilter`, `KaleidoCorrelationEndpointFilter`, `KaleidoJsonEndpointFilter` and `KaleidoCallerContextEndpointFilter`.
- `KaleidoHttpServiceCollectionExtensions` in `Kaleido.Http.Server` provides `AddHttp()`; it registers routing, `IHttpContextAccessor` and the active HTTP execution adapters. The host configures its own middleware pipeline (#295).
- `HttpCorrelationContextReader` in `Kaleido.Server` sanitizes inbound HTTP headers into `KaleidoCorrelationContext`.

#### HTTP transport services

- `ProcessExecutionService` — translates HTTP execute requests into runtime `ProcessRequest` values and writes the resolved `ProcessId` into the response headers
- `ProcessStateService` — reads durable process state and maps it to the HTTP response contract

#### HTTP transport conversion

- `QueryBodyResolver` — converts `QueryApiBody` (HTTP transport shape, string enums + `JsonElement` values) to `QueryBody` (runtime shape, typed CLR values + enums) before handing to core. Uses `QueryableFieldDescriptor.FieldType` for value resolution.
- `KaleidoJsonEndpointFilter` — `IEndpointFilter` applied to all Kaleido route groups; wraps `IValueHttpResult` responses in `Results.Json(..., KaleidoJsonOptions.Options)` so enums serialize as strings without touching global `JsonOptions`.
- `KaleidoJsonOptions.Options` — shared `JsonSerializerOptions` in `Kaleido.Platform`, used by both server filter and client.

#### Queryable endpoint mapping (`QueryableEndpointRouteBuilderExtensions`)

- `POST /{service}/{version}/queryable/{source}/query` — direct query of a local or delegated source
- `POST /{service}/{version}/queryable/{source}/{view}/query` — view query

#### Process endpoint mapping (`ProcessorEndpointRouteBuilderExtensions`)

- `POST /{service}/{version}/processes/execute` — multi-step execute
- `GET /{service}/{version}/processes/{processId}` — process state
- `POST /{service}/{version}/processes/steps/{step}` — per-step execute
- The central `GET /registry` publishes these URLs and the process-state URL template; there is no per-step metadata endpoint.

#### Control registry and participation

- `Kaleido.Registry` owns registration, leases, store-backed merged views, transport bindings and HybridCache (`Kaleido:Registry:Cache`). `Kaleido.Registry.Http` maps its fixed `/registry` API through `ControlRegistryEndpointRouteBuilderExtensions`.
- Services register through `POST /registry/instances`, renew leases and deregister. `GET /registry` filters for ordinary callers; participants hydrate unfiltered `GET /registry/availability` and revisioned `GET /registry/capabilities` conditionally. `GET /registry/status` exposes operations status.
- `MapKaleidoHttp()` maps only this service's capability endpoints and requires an `IKaleidoRegistrySource`: business hosts use `AddControlRegistryClient()`, while a registry host has its own store-backed source. `CentralRegistrySource` in `Kaleido.Client` keeps participating hosts' unfiltered last-good copy; `KaleidoRegistryService` in `Kaleido.Server` filters discovery per caller. Neither polls another service.

#### HTTP URL generation

- `KaleidoHttpRoutePrefix`, `ProcessContractUrls` / `ProcessRoutePaths` and `QueryableContractUrls` live in `Kaleido.Http` (`src/Http/Http`); only the server publishes capability URLs. Clients use the published ones.

### Server and HTTP endpoint invariants

- Endpoints adapt contracts and publish routes. They do not reimplement runtime planning or business execution.
- The only service route prefix is `/{ServiceName}/{ServiceVersion}` from `KaleidoServiceOptions`; `ServiceVersion` is required by HTTP mapping. The gateway maps that logical public route to the one host serving the version.
- Registry endpoint returns 200 by default. Partial responses are signalled through `Errors`/`IsPartial`; `?strict` opts into a 502 when partial.
- `ConfigureHttpJsonOptions` is never called — Kaleido JSON options are scoped to Kaleido endpoints via `KaleidoJsonEndpointFilter`. Consumer endpoints retain their own serialization behavior.

---

## 3. Kaleido.Platform and Kaleido.Http

### Platform contract structure

#### Process contracts

- Request: `ExecuteProcessRequest`, `ExecuteStepRequest<TStep>`
- Response: `ProcessExecutionResponse`, `ProcessExecutionStepResponse`, `StepExecutionResponse`, `StepExecutionResponse<TResult>`, `ProcessStateResponse`
- Reference types: `ProcessRequiredStep`, `ProcessStepSummary`, `ProcessorRegistryResponse`, `ProcessStepResponse`

#### Queryable contracts

- Request: `QueryApiRequest`, `QueryApiRequest<TParameters>` — accepts `QueryApiBody` (transport shape)
- Transport body: `QueryApiBody`, `QueryApiFilterNode`, `QueryApiFilterCondition`, `QueryApiFilterGroup`, `QueryApiSort`, `QueryApiPage` — string enums, raw `JsonElement` filter values
- Response: `QueryableSourceResponse` (carries `ServiceName`, `ServiceVersion` and `QueryUrl`), `QueryableFieldMetadata`, `QueryableViewResponse`; errors use the shared `KaleidoErrorResponse` in `Kaleido.Platform`.
- `QueryApiBodyExtensions.ToApiBody()` — converts runtime `QueryBody` → `QueryApiBody` for core callers forwarding over HTTP (e.g. delegated query sources)
- `KaleidoJsonOptions.Options` — shared `JsonSerializerOptions` with `JsonStringEnumConverter` for all Kaleido HTTP serialization

### Platform contract invariants

- `Kaleido.Platform` owns the supported model and wire contract surface shared by `Kaleido.Http.Server`, `Kaleido.Http.Client` and future protocols; `Kaleido.Http` holds only shared HTTP routes.
- Treat the published model as a public API boundary. Prefer additive changes.
- `QueryApiBody` and its sub-types are the wire shape — string enums, `JsonElement` values. Core `QueryBody`/`QueryFilterNode`/`FilterOperator` are never serialized to HTTP directly.
- `KaleidoJsonOptions.Options` is the single `JsonSerializerOptions` for all Kaleido HTTP serialization — server responses and client read/write. All `ReadFromJsonAsync`/`JsonContent.Create` calls in Kaleido code must pass it explicitly.

---

## 4. Kaleido.Client and Kaleido.Http.Client

### Client and HTTP adapter structure

#### Process client

- `IKaleidoProcessorClient` in `Kaleido.Client` is the neutral contract for process state and step execution. `KaleidoProcessorClient` in `Kaleido.Http.Client` sends the URLs the unfiltered central registry publishes; correlation and idempotency are stamped by the HTTP transport. Failed HTTP responses raise `KaleidoHttpClientException`.

#### Queryable client

- `IKaleidoQueryableClient` in `Kaleido.Client` queries sources and views by published name; `KaleidoQueryableClient` sends their published `QueryUrl` through the one gateway client.

#### Client registry participation

- `Kaleido.Client` owns `IKaleidoRegistryClient`, the registration/heartbeat loop (`ControlRegistryRegistrar`), the hydrated copy (`CentralRegistrySource` and `CentralRegistrySync`) and its readiness check (`RegistrationHealthCheck`). None of these orchestrators knows HTTP routes or response types.
- `Kaleido.Http.Client` implements the wire client as `KaleidoHttpRegistryClient` and wires it with `AddControlRegistryClient()`, using the gateway address in `Kaleido:BaseUrl`. Registry call failures cross the neutral seam as `KaleidoRegistryClientException`.

#### Shared client services (#498)

Every piece is built by the container; no service creates another.

- `KaleidoRegistryTargetResolver` reads the hydrated unfiltered registry and selects the current service version, or the explicit version for an existing process. `KaleidoTransportSelector` represents selection and safe-fallback policy, but only HTTP is currently supplied to the resolver; the client send path does not yet invoke cross-protocol fallback.
- `AddHttpClients()` registers one named gateway `HttpClient` using `Kaleido:BaseUrl`. `IKaleidoProcessorClientFactory` and `IKaleidoQueryableClientFactory` create clients by service name, optionally pinned to a version; keyed injection also resolves by service name without an endpoint list.
- `KaleidoOnBehalfOfGuard` enforces what an outbound call carries; `KaleidoClientHandlerFilter` adds the host's `AddKaleidoClientHandler<T>()` handlers to the gateway and registry clients. `IKaleidoClientTransport` sends through the gateway with correlation headers, idempotency, logging, failure mapping and deserialization.

### Client and HTTP adapter invariants

- Clients do not discover or cache registries independently. The host's one central-registry copy supplies the published routes for Process and Queryable calls and MCP; the HTTP adapter validates each relative URL under its version's logical gateway route before sending.
- Both clients automatically forward Kaleido correlation headers on outbound requests.
- A service maps only `/{service}/{version}/...`; the router maps each versioned public route to that version's host, without putting instance addresses in the registry.

---

## 5. Kaleido.Observability.OpenTelemetry

### OpenTelemetry provider structure

- `KaleidoObservabilityOpenTelemetryExtensions` — `AddOpenTelemetry()` on `IKaleidoBuilder` (logging + tracing + metrics + OTLP) and `AddKaleidoInstrumentation()` on `TracerProviderBuilder`/`MeterProviderBuilder`
- Instrumentation itself stays in `Kaleido` (BCL `ActivitySource`/`Meter`); this project only wires the OTel SDK

### OpenTelemetry provider invariants

- Core is observability-provider-agnostic; this is one possible `Kaleido.Observability.<Technology>` provider.

---

## 6. Kaleido.Provider.SQLite

### SQLite provider structure

- `SqliteKaleidoContextStore`, `SqliteKaleidoOutboxStore`, `SqliteKaleidoIdempotencyStore` - implement the store contracts with EF Core on one scoped `SqliteKaleidoContextDbContext`; `SqliteKaleidoTransactions` opens one transaction on it per process-state transition, so the context store and the outbox store (or a host's outbox joining `ISqliteKaleidoTransaction`) commit together
- `SqliteKaleidoServiceCollectionExtensions` - `AddSqliteContextStore` (+ the transactions), `AddSqliteOutboxStore`, `AddSqliteIdempotencyStore`: one store each, one database

### SQLite provider invariants

- Each SQLite registration replaces one in-memory default registered by `AddKaleido()`; all share one database (one connection string).
- The store must correctly implement state reconciliation so that existing saved contexts remain valid when the step registry changes.

---

## 7. Kaleido.Mcp

### MCP adapter structure

- `KaleidoMcpServiceCollectionExtensions` — `AddMcp()`: the MCP server (stateless Streamable HTTP), `KaleidoMcpOptions` (configuration over code, ADR 0030), the tool catalog and the tool invoker; startup fails without `AddHttp()`, `AddControlRegistryClient()`, `AddHttpClients()` or `Kaleido:OnBehalfOfCalls = Exchange`
- `KaleidoMcpEndpointRouteBuilderExtensions` — `MapKaleidoMcp(pattern)`; the endpoint runs the same correlation setup as Kaleido's HTTP endpoints
- `KaleidoMcpToolHandler` — the server's hooks: `tools/list` returns the caller's tools, `tools/call` runs one
- Tool catalog: `ToolCatalogProvider` (the caller's tools from the central registry's per-caller filtered view), `ToolCatalogBuilder` (registry to tools, incl. the front-door menu), `ToolCatalogCache` (translated catalogs by registry revision, per registration, #464), `ToolNameBuilder` (`{service}_{capability}`, independent of what the caller sees, #435), `ToolSchemaBuilder` (input metadata to JSON Schema)
- `KaleidoToolInvoker` — one tool call: a query or a step through the Kaleido clients; its parts handle the confirmation round (#439), the front door (`search_tools`, `describe_tool`, `next`) and guided lookups
- `InformationRequestElicitor` — information requests as MCP forms (elicitation, or an MRTR round) and the answers back
- `McpStateProtector` / `McpKeyRingCheck` — every state round-tripped through the client (guided lookup, answers form, answersToken, confirmation) is protected with Data Protection, one purpose per kind (#516); a startup warning when the key ring would not be shared across instances (#452, #484)
- `McpObservability` — the adapter's traces, metrics and boundary logs (`McpTelemetry` names)

### MCP adapter invariants

- Tools are generated from the registry, never declared: a capability is a tool because the service publishes it, and the list is the caller's (a tool not in it is `mcp_unknown_tool`, and no service is called).
- Every tool call goes through the Kaleido clients **as the caller**: it carries the token the host's handler exchanged for that caller (`Kaleido:OnBehalfOfCalls = Exchange`, required), never the agent's token and never the host's own identity (#574).
- The agent and the MCP client are untrusted input: authorization, validation and confirmation are decided on the server ([trust boundary](./Mcp/Mcp/README.md#trust-boundary)).
- The adapter has no wire client logic of its own; it references `Kaleido.Http.Server` and `Kaleido.Http.Client`. Its tool calls use the client's registry-selected service version.

---

## Cross-cutting concerns

### Correlation identity

`KaleidoCorrelationContext` flows through all layers:

- initialized from HTTP headers by `HttpCorrelationContextReader` / `KaleidoCorrelationEndpointFilter` on Kaleido's endpoints (in `Kaleido.Server`; the MCP adapter runs the same setup for its endpoint)
- accessed via the scoped `IKaleidoCorrelationContextAccessor` (registered by `AddKaleido()` in `Kaleido`)
- forwarded by HTTP clients as outbound headers (in `Kaleido.Http.Client`)
- included as tags on observability activities (in `Kaleido`)

#### Correlation context invariants

These are framework invariants — code that violates them produces split traces:

- **A runtime's own scope starts from the request's correlation** — a step handler
  (`ProcessorStepInvoker`) and a query's sources and views (`QueryableService`) each run in their
  own DI scope, initialized with the request's `KaleidoCorrelationContext` (the handler's with its
  process id and executing step). An empty scope would lose the caller, the request id and the
  process id: no record-level filtering by caller, no forwarding of the ids downstream (#413).
  The scope also gets the request's **inbound caller** (`IKaleidoInboundCallerAccessor`: principal
  and presented credential), so a call the handler or source makes is made for that caller
  (`OnBehalfOfCaller`, ADR 0022). Without it every such call goes out as the service's own: an
  anonymous caller would reach the downstream with the service's credential, and a step that
  requires a user behind the call would refuse it (#428). Outside a request (the event relay, a
  hosted service) there is no inbound caller, and the calls stay the service's own.
- **Exactly one RequestId per HTTP request** — `KaleidoCorrelationEndpointFilter` echoes the inbound
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

`IEventPublisher` belongs to the relay: `AddOutboxRelay<TPublisher>()` registers it with the relay, on one host per outbox database (a service, or a host with nothing else). Services register none (ADR 0036).

Events are handed to `IEventPublisher` **at least once, never on the request path** ([ADR 0012](../docs/decisions/0012-events-at-least-once-through-the-store.md)):

- **Kaleido owns when; services write, a relay publishes** (ADR 0023, ADR 0036). The runtime hands every event to the outbox (internal `IKaleidoOutbox` over `IKaleidoOutboxStore`), tagged with the writing service and version. A transition's event goes inside its transaction (`ProcessorTransition`: begin, save the state, add the event, commit, committed): `ProcessCreated` (a new process is saved right away) and `StepCompleted`. `PlanBuilt`, `ExecutionCompleted` and `QueryExecuted` are added right after, without a transaction; a failure is logged and counted, never fails the request. Every service writes every event; publishing is the relay's (`AddOutboxRelay`), which drains every entry of its outbox store's database.
- **Relay.** `KaleidoEventRelay`, a hosted background service registered by `AddKaleido()` (one per instance), claims a batch (`Kaleido:Events:BatchSize`), publishes each entry through a Polly pipeline (timeout + circuit breaker, no Polly retries; `Kaleido:Events:Resilience`) and removes it on success. A failure is retried with exponential backoff (the durable schedule lives in the outbox, so it survives restarts); after `MaxRetryAttempts` the entry is parked. A write wakes the local relay at once; otherwise it polls, backing off to 10 s while the outbox is empty. Claims are leases, so an entry is in one instance's hands at a time; when an instance stops, its leases expire and another takes over.
- **Privacy.** Payloads are complete (the audit trail); before the outbox write, the registered `IKaleidoEventRedactor`s run and properties marked with a `DataClassificationAttribute` are redacted per the event policy (`AddEventRedaction`; the host's `AddRedaction` stays the log policy), or erased without one (`KaleidoEventPayloadWriter`, ADR 0013, ADR 0025). A crash between a successful publish and its removal publishes the event again - consumers drop duplicates by `IKaleidoEvent.EventId`.

### Discoverability

Metadata is derived from CLR types at startup:

- `TypeDescriber` (internal) converts CLR property types to `DataTypeDescriptor` values
- `ConstraintMapper` (internal) converts `ValidationAttribute` usage to `ConstraintContract` values
- Both are used by the Queryable and Process runtimes when building discovery metadata for HTTP endpoints
- The runtime registry descriptors (`DataTypeDescriptor`, `ConstraintContract`, the `Kaleido.Processor.Registry` and `Kaleido.Queryable.Metadata` types) are internal. The published contracts in `Kaleido.Platform` have their own types (`DataTypeMetadata`, `ConstraintMetadata`, `ProcessFieldLookup`, `QueryablePagingMetadata`, …), and `Kaleido.Http.Server` maps to them (`RegistryMetadataMapper`, `ProcessorResponseFactory`, `QueryableRegistryItemExtensions`), so a change to a core descriptor is never a wire change by accident (#356)
