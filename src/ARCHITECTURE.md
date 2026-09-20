# Kaleido Source Architecture

This document describes the architecture of the `src/` projects in detail. It covers project dependencies, internal structure, and the key design decisions within each project.

For the top-level repository model, see [`../ARCHITECTURE.md`](../ARCHITECTURE.md).

---

## Project dependency graph

```
Kaleido.Http.Client ──────────────────────────────────┐
                                                       ↓
Kaleido.Http ──────────────────────────────────► Kaleido.Http.Abstractions
                  ↑                                    ↑
Kaleido.AspNetCore ─────────────────────────────────── │
                  ↑                                    │
             Kaleido ◄──────────────────────────────────┘

Kaleido.Provider.SQLite ──► Kaleido
```

- `Kaleido` has no Kaleido project dependencies — it is the foundation.
- `Kaleido.AspNetCore` depends on `Kaleido` and `Kaleido.Http.Abstractions`.
- `Kaleido.Http` depends on `Kaleido.AspNetCore` and `Kaleido.Http.Abstractions`.
- `Kaleido.Http.Client` depends on `Kaleido` and `Kaleido.Http.Abstractions`.
- `Kaleido.Provider.SQLite` depends on `Kaleido` only.

---

## 1. Kaleido (core)

### Internal structure

The core project is organized into two main namespaces:

**`Kaleido` (bootstrap and shared)**
- `KaleidoServiceCollectionExtensions` — `AddKaleido()` / `AddAssembly(...)`
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
- `AddAssembly(...)` records assemblies; it does not scan them for capabilities. Scanning happens at `AddQueryable()` / `AddProcessor(...)` time.
- `QueryableService` dispatch order (delegated → local → direct) is a published semantic and must not change casually.
- `ProcessorContext` is current resumable state only, not an audit log.
- `IKaleidoBuilder` is intentionally minimal.

---

## 2. Kaleido.AspNetCore

### Internal structure

**Shared infrastructure**
- `ExceptionMiddleware` — catches `KaleidoFrameworkException` (500), `ArgumentException` (400)
- `KaleidoAspNetCoreCorrelation` / `KaleidoAspNetCoreHeaders` — correlation-header parsing
- `ApiErrorContract` — shared error response shape

**Queryable transport**
- `QueryableAspNetCoreServiceCollectionExtensions` — `AddQueryableAspNetCore(...)` builder extension
- `QueryableValueNormalizer` — normalizes incoming filter values to CLR types before query execution

**Process transport**
- `ProcessAspNetCoreServiceCollectionExtensions` — `AddProcessorAspNetCore(...)` builder extension
- `ProcessExecutionService` — translates HTTP execute requests into runtime `ProcessRequest` values and writes the resolved `ProcessId` into the response headers
- `ProcessStateService` — reads durable process state and maps it to the HTTP response contract

### Key design invariants
- This project adds DI registrations and transport services. It does not map HTTP routes.
- `ExceptionMiddleware` is targeted — it does not catch every possible exception type.
- `QueryableValidationException` subtypes are caught at the Queryable endpoint level in `Kaleido.Http`, not in this middleware.

---

## 3. Kaleido.Http

### Internal structure

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

## 4. Kaleido.Http.Abstractions

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

## 5. Kaleido.Http.Client

### Internal structure

**Process client**
- `IKaleidoProcessClient` — typed interface: `GetRegistryAsync`, `GetStepMetadataAsync`, `GetProcessStateAsync`, `ExecuteAsync`, `ExecuteStepAsync`, `ExecuteStepAsync<TStep, TResult>`
- `KaleidoProcessClient` — concrete implementation; lazily fetches and caches the remote registry per client instance
- `KaleidoProcessClientException` — thrown on non-success responses and on registry lookup failures
- `KaleidoProcessClientServiceCollectionExtensions` — `AddProcessClient(name, baseUrl)` and `AddProcessClients(names...)`

**Queryable client**
- `IKaleidoQueryableClient` — typed interface: `GetRegistryAsync`, `GetContextMetadataAsync`, `QueryViewAsync`, `QueryContextAsync`
- `KaleidoQueryableClient` — concrete implementation; lazily fetches and caches the remote registry per client instance
- `KaleidoQueryableClientException` — thrown on non-success responses and on registry lookup failures
- `KaleidoQueryableClientServiceCollectionExtensions` — `AddQueryableClient(name, baseUrl)` and `AddQueryableClients(names...)`

### Key design invariants
- Both clients lazily fetch and cache the remote registry for the lifetime of the client instance.
- Both clients automatically forward Kaleido correlation headers on outbound requests.
- `AddProcessClients` / `AddQueryableClients` reads base URLs from `Kaleido:Clients:<Name>:BaseUrl`, with a `Kaleido:BaseUrl` fallback.
- The client name is lowercased to derive the route prefix, matching the remote server's `Kaleido:ServiceName`.

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
- initialized from HTTP headers by `KaleidoAspNetCoreCorrelation` (in `Kaleido.AspNetCore`)
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
