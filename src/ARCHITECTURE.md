# Kaleido Source Architecture

This document describes the architecture of the `src/` projects in detail. It covers project dependencies, internal structure, and the key design decisions within each project.

For the top-level repository model, see [`../ARCHITECTURE.md`](../ARCHITECTURE.md).

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
- `DataTypeMapper` — CLR type → `DataTypeDescriptor` projection
- `ConstraintMapper` — `ValidationAttribute` → `ConstraintContract` projection
- `KaleidoCorrelationContext` — shared ambient identity
- `IEventPublisher` / `NullEventPublisher` — infrastructure-agnostic event seam
- `KaleidoEnumConverter` / `ValueConverter` — JSON and value conversion helpers

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
- State: `IProcessContextStore`, `InMemoryProcessContextStore`, `ProcessorContext`
- Registries: `ProcessStepRegistry`, `ProcessorRegistry`
- Observability: `ProcessObservability`

### Key design invariants
- The core project has no transport dependencies.
- `KaleidoServiceOptions.Assemblies` records assemblies; recording does not scan them for capabilities. Scanning happens during the `AddQueryable()` / `AddProcessor()` calls that `AddKaleido()` invokes internally.
- `QueryableService` dispatch order (delegated → local → direct) is a published semantic and must not change casually.
- `ProcessorContext` is current resumable state only, not an audit log.
- `IKaleidoBuilder` is intentionally minimal.

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

**Queryable endpoints** (`QueryableEndpointRouteBuilderExtensions`)
- `GET /{prefix}/queryable` — catalog
- `GET /{prefix}/queryable/registry` — full registry
- `GET /{prefix}/queryable/{context}/{metadataRoute}` — per-context metadata
- `POST /{prefix}/queryable/{context}/{queryRoute}` — direct context query
- `POST /{prefix}/queryable/{context}/{view}/{queryRoute}` — view query

**Process endpoints** (`ProcessEndpointRouteBuilderExtensions`)
- `GET /{prefix}/processes` — processor catalog (initial steps only)
- `GET /{prefix}/processes/steps` — step catalog (all steps, lightweight)
- `GET /{prefix}/processes/registry` — full registry (all step metadata)
- `GET /{prefix}/processes/steps/{step}/metadata` — per-step metadata
- `POST /{prefix}/processes/execute` — multi-step execute
- `GET /{prefix}/processes/{processId}` — process state
- `POST /{prefix}/processes/steps/{step}` — per-step execute

**Registry endpoint** (`RegistryEndpointRouteBuilderExtensions`)
- `GET /{prefix}/registry` — aggregated discovery (local process + all downstream process clients + all downstream queryable clients)
- Always returns HTTP 200; unreachable downstream clients populate `ClientErrors`

**URL generation**
- `ProcessContractUrls` / `ProcessRoutePaths`
- `QueryableContractUrls` / `QueryableRoutePaths`

### Key design invariants
- Endpoints adapt contracts and publish routes. They do not reimplement runtime planning or business execution.
- The route prefix is derived from `KaleidoServiceOptions.ServiceName` (bound from `Kaleido:ServiceName` configuration).
- Registry endpoint always returns 200. Partial responses are signalled through `ClientErrors`, not through HTTP error status codes.

---

## 3. Kaleido.Http.Abstractions

### Internal structure

**Process contracts**
- Request: `ExecuteProcessRequest`, `ExecuteStepRequest<TStep>`
- Response: `ProcessExecutionResponse`, `ProcessExecutionStepResponse`, `StepExecutionResponse`, `StepExecutionResponse<TResult>`, `ProcessStateResponse`
- Reference types: `ProcessStepInfo`, `ProcessStepSummary`, `ProcessorRegistryResponse`, `ProcessStepResponse`

**Queryable contracts**
- Request: `QueryApiRequest`, `QueryApiRequest<TParameters>`, `QueryBody`, `QueryPage`, `QueryFilterNode`
- Response: `QueryableRecordResponse`, `QueryableRecordSummary`, `QueryErrorResponse`

### Key design invariants
- This project defines the published HTTP contract surface shared by server (`Kaleido.Http`) and client (`Kaleido.Http.Client`).
- Treat it as a public API boundary. Prefer additive changes.

---

## 4. Kaleido.Http.Client

### Internal structure

**Process client**
- `IKaleidoProcessClient` — typed interface: `GetRegistryAsync`, `GetStepMetadataAsync`, `GetProcessStateAsync`, `ExecuteAsync`, `ExecuteStepAsync`, `ExecuteStepAsync<TStep, TResult>`
- `KaleidoProcessClient` — concrete implementation; lazily fetches and caches the remote registry per client instance
- `KaleidoProcessClientException` — thrown on non-success responses and on registry lookup failures
- `KaleidoProcessClientServiceCollectionExtensions` — internal `AddProcessClient(...)` registration used by `AddHttpClients`

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
- `SqliteProcessContextStore` — implements `IProcessContextStore` using SQLite via EF Core
- `SqliteProcessContextStoreServiceCollectionExtensions` — `UseSqliteProcessContextStore(connectionString)` extension; replaces the default in-memory store

### Key design invariants
- Calling `UseSqliteProcessContextStore(...)` replaces the in-memory `IProcessContextStore` registered by `AddProcessor(...)`.
- The store must correctly implement state reconciliation so that existing saved contexts remain valid when the step registry changes.

---

## Cross-cutting concerns

### Correlation identity
`KaleidoCorrelationContext` flows through all layers:
- initialized from HTTP headers by `HttpCorrelationContextReader` / `ObservabilityMiddleware` (in `Kaleido.Http`)
- accessed via the scoped `IKaleidoCorrelationContextAccessor` (registered by `AddKaleido()` in `Kaleido`)
- forwarded by HTTP clients as outbound headers (in `Kaleido.Http.Client`)
- included as tags on observability activities (in `Kaleido`)

### Observability
Both Queryable and Process publish observability through activity sources and meters:
- Queryable: `Kaleido.Queryable` activity source and meter
- Process: `Kaleido.Process` activity source and meter (names defined in `ProcessTelemetry`)

### Event publishing
`IEventPublisher` is registered by `AddKaleido()` as a no-op `NullEventPublisher` by default. Replace it before calling `AddKaleido()` to install real event infrastructure.

### Discoverability
Metadata is derived from CLR types at startup:
- `DataTypeMapper` converts CLR property types to `DataTypeDescriptor` values
- `ConstraintMapper` converts `ValidationAttribute` usage to `ConstraintContract` values
- Both are used by the Queryable and Process runtimes when building discovery metadata for HTTP endpoints
