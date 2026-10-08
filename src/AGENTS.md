# Kaleido source contributor guide

This is the contributor guide for `src/`. Read it before making changes to any Kaleido source project.

See also:
- [`ARCHITECTURE.md`](./ARCHITECTURE.md) — source-level architecture details
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — repo-level architecture overview
- [`../AGENTS.md`](../AGENTS.md) — repo-level contributor guide

---

## Scope and boundaries

### `src/Kaleido`
Owns the core runtime:
- root bootstrap (`AddKaleido()`, `IKaleidoBuilder`, `KaleidoServiceOptions.Assemblies`)
- shared metadata primitives (`TypeDescriber`, `ConstraintMapper`)
- shared eventing abstractions and correlation context
- shared JSON/value-conversion helpers
- Queryable runtime: context/view registration, validation, dispatch, execution, observability
- Process runtime: step registration, planning, execution, state mutation, observability
- Default in-memory `IProcessorContextStore`

Does **not** own HTTP endpoints, ASP.NET Core DI, HTTP contracts, remote client consumption, or SQLite persistence.

### `src/Kaleido.Http`
Owns the full HTTP transport layer — middleware, correlation propagation, and endpoint publication:
- `AddHttp()` — public entry point; registers routing, `IHttpContextAccessor`, the middleware pipeline, and HTTP execution services
- `ExceptionMiddleware` — outermost middleware; maps exceptions to JSON error responses
- `ObservabilityMiddleware` — reads inbound correlation headers via `HttpCorrelationContextReader`, populates `IKaleidoCorrelationContextAccessor`, tags the `Activity`, and echoes `RequestId`/`ProcessId` on the response
- `HttpCorrelationContextReader` — reads and sanitizes inbound HTTP headers into `KaleidoCorrelationContext`
- `KaleidoStartupFilter` — registers middlewares via `IStartupFilter` in the correct pipeline order
- `MapQueryable()` — all Queryable HTTP endpoints
- `MapProcessor()` — all Process HTTP endpoints
- `MapRegistry()` — internal; `GET /{service}/registry` unified discovery endpoint (mapped by `MapKaleidoHttp`, optionally aggregating downstream clients)
- `IProcessExecutionService` / `ProcessExecutionService` — translates HTTP execute requests into runtime calls
- `IProcessStateService` / `ProcessStateService` — reads durable process state and maps it to HTTP contracts

Depends on `Kaleido.Http.Abstractions` only. Does **not** own runtime logic or core DI registration.

### `src/Kaleido.Http.Abstractions`
Owns shared HTTP contract types and HTTP-specific correlation primitives:
- `KaleidoCorrelationHeaders` — canonical `X-Kaleido-*` header name constants (namespace `Kaleido.Http`)
- `HttpHeaderSanitizer` — RFC 7230-compliant sanitization, max 256 chars, printable ASCII only
- Process HTTP request/response contracts
- Queryable HTTP request/response contracts

Changes here ripple into `Kaleido.Http` (server) and `Kaleido.Http.Client` (client). Prefer additive changes.

### `src/Kaleido.Http.Client`
Owns typed HTTP clients for consuming remote Kaleido services:
- `IKaleidoProcessorClientFactory` / `KaleidoProcessorClient`
- `IKaleidoQueryableClientFactory` / `KaleidoQueryableClient`
- `AddHttpClients()` — consumer-facing registration (config-driven `Kaleido:Clients`); `AddProcessorClient(...)`/`AddQueryableClient(...)` are internal

Does **not** own server-side runtime logic or endpoint mapping.

### `src/Kaleido.Observability.OpenTelemetry`
Owns the OpenTelemetry observability provider (opt-in, no OTel dependency in core):
- `AddOpenTelemetry()` on `IKaleidoBuilder` — one-call OTel setup: logging + tracing + metrics + OTLP export
- `AddKaleidoInstrumentation()` on `TracerProviderBuilder` — registers Kaleido `ActivitySource`s
- `AddKaleidoInstrumentation()` on `MeterProviderBuilder` — registers Kaleido `Meter`s with tuned histogram Views
- Does **not** own core instrumentation — `ActivitySource`/`Meter` definitions stay in `Kaleido`

Future observability providers follow the same pattern: `Kaleido.Observability.<Technology>`.

### `src/Kaleido.Provider.SQLite`
Owns the reference `IProcessorContextStore` implementation (SQLite):
- `UseSqliteProcessorContextStore(...)` extension
- SQLite-backed `IProcessorContextStore` — a worked example only; consumers implement `IProcessorContextStore` against their own durable infrastructure

---

## Core runtime rules

### Bootstrap rules
`AddKaleido()` should remain responsible for:
- shared DI baseline setup
- scoped correlation accessor registration (`TryAddScoped` so pre-existing registrations win)
- default event publisher registration
- returning the builder for higher-level frameworks

`AddAssembly(...)` is a lightweight recording step only. Core does not itself scan assemblies for capability registrations.

### Builder rules
`IKaleidoBuilder` is intentionally small: `Services`, `Assemblies`, `Configuration` and `ServiceOptions` — what every subsystem builder (Process, Queryable, Registry, transports) needs without a DI lookup. Be cautious about expanding it — additional members affect every framework built on top.

`KaleidoBuilder` deduplicates assemblies by identity. Preserve that behavior.

### Shared metadata rules
`DataTypeMapper` and `ConstraintMapper` are high-impact shared primitives. Changes ripple into anything that reflects CLR types into discovery metadata, projects validation rules, or converts transport values.

Be careful when changing:
- scalar type names or format strings
- nullability behavior
- enum metadata behavior
- constraint naming or parameter conventions
- conversion error behavior

### Queryable runtime rules
Registration invariants:
- **The source is the identity; the query context describes how it is queried.** Discovery is interface-only:
  - sources: `IQuerySource<T>`, `IQuerySourceAsync<T>`, `IDelegatedQuerySource<…>`;
  - views: `IQueryViewSource<TSource, …>`, `IQueryViewSourceAsync<TSource, …>`.

  Every source needs `[QuerySource]` and every view `[QueryView]`, each with a non-empty `Version`, `DisplayName` and `Description`.
- **Names are type names:** source names are unique per service; view names are unique per source.
- **A type is exactly one capability:** one source shape (sync xor async, one query context; local xor delegated) or one view shape. A view must reference a registered local source.
- **A query context may back several sources.** Sources are registered and resolved by their concrete type, never as `IQuerySource<T>`.

Runtime invariants:
- **`QueryableService` dispatches by the queried type** (view, local source, delegated source). Because each type is exactly one capability, there is no lane order or fallback.
- **The consumer's query is validated against the source's query context before execution, delegated sources included.** A delegated source's `QueryResult` (including its paging) is returned untouched.
- **`QuerySourceKind` (local or delegated) is service-side runtime metadata.** It must not be published on the registry or wire contracts; consumers see every source the same way.
- **Paging is per capability:** the source's `[Pageable]` applies to direct queries, a view's to that view; it is never inherited. A pageable view must declare a sortable `DefaultSortField`.

### Process runtime rules
Process is step-centric, not query-centric. Do not import Queryable terminology into Process code or docs.

Registration invariants:
- `AddKaleido()` must be called before `AddProcessor(...)`
- A step is identified by `IProcessStep`; `[ProcessStep]` only describes it. Every `IProcessStep` type needs `[ProcessStep]` with a non-empty `Version`, `DisplayName`, and `Description`, and `[ProcessStep]` is only valid on `IProcessStep` types
- A step's name is its type name (`Type.Name`, unmodified); step type names must be unique across the assembly scan
- Inside the runtime the next step is typed (`Success<TNext>()`, `Type` through invoker → evaluator); step names are produced only at the boundary (state, events, responses)
- Every discovered step must have exactly one handler
- Relationship graphs must not self-reference or be circular

The planning pipeline is layered — keep those responsibilities separated:
- `StepCandidateBuilder` → resolves step names, hydrates input
- `StepCandidateValidator` → enforces `DataAnnotations`
- `StepCandidateConsistencyChecker` → enforces history, dependency, and repeatability consistency
- `StepCandidatePlanner` → orders executable candidates
- `ExecutionProcessor` → invokes handlers, evaluates decisions, updates state, persists, emits events

`ProcessorContext` is current resumable state only. Keep it small. Historical evidence belongs in emitted process events.

---

### Telemetry conventions
Instrument names and tag keys live in the `*Telemetry` constants classes
(`ProcessorTelemetry`, `QueryableTelemetry`, `KaleidoHttpTelemetry`) — constants
only, no `Meter`/`Counter` instances. Each emitting class owns its
instruments as `private static readonly Meter`/`Counter`/`Histogram` fields
created with the shared `MeterName`; when two classes emit the same logical
signal (e.g. `endpoint_errors` from `ExceptionMiddleware` and the
authorization result handler), each declares its own counter with the same
instrument name — the meter-name subscription merges them.

## Transport and HTTP rules

### Kaleido.Http.Abstractions stability
This is a shared contract boundary. Treat it like a public API:
- prefer additive changes (new optional fields) over breaking changes
- when a contract must change, update the matching endpoint, client method, and tests together

### Kaleido.Http should stay thin
Middleware and endpoint mapping code should adapt contracts and wire the runtime. It should not reimplement runtime planning or execution logic that belongs in `Kaleido`.

### Transport contract boundary
Core runtime types (`QueryBody`, `QueryFilterNode`, `FilterOperator`, `SortDirection`, `LogicalOperator`) are never serialized over HTTP directly. `QueryApiBody` and its sub-types in `Kaleido.Http.Abstractions` are the wire contract — string enums, `JsonElement` values. `QueryBodyResolver` (in `Kaleido.Http`) converts between them before handing to core.

`KaleidoJsonEndpointFilter` is applied to all Kaleido route groups — it wraps `IValueHttpResult` responses in `Results.Json(..., KaleidoJsonOptions.Options)` so enums serialize as strings. Do not call `ConfigureHttpJsonOptions` — it affects all endpoints in the host app, not just Kaleido's.

`KaleidoJsonOptions.Options` (in `Kaleido.Http.Abstractions`) is the single shared `JsonSerializerOptions` for all Kaleido HTTP serialization. Every `ReadFromJsonAsync`/`JsonContent.Create` call in Kaleido code must pass it explicitly.

`QueryResult<T>.TotalCount` semantics: equals `results.Count` when `page` is absent from the request (caller got all results, no count query ran), or when `page` is present but the returned page is partial (`results.Count < page.Size`). Only when `page` is provided AND the page is full does `TotalCount` equal the true unfiltered count — `CountAsync` runs only in that case.

When a delegated query source (`IDelegatedQuerySource`) receives a `QueryBody` and needs to forward it over HTTP, use `query.ToApiBody()` from `Kaleido.Http.Abstractions` — samples and consumers should only reference `Kaleido.Http.Abstractions`, not `Kaleido.Http` (which carries ASP.NET Core dependencies).

---

## Correlation rules
`KaleidoCorrelationContext` fields are transport-agnostic — they live in `Kaleido` core. The HTTP wire names (`KaleidoCorrelationHeaders`) live in `Kaleido.Http.Abstractions`. Changes to either affect transport handling, observability, event payloads, and higher-level runtime context propagation. Treat them as broad-impact changes.

---

## Eventing rules
`IEventPublisher` is intentionally infrastructure-agnostic. The default `NullEventPublisher` makes eventing optional. Do not remove that default.

---

## What not to change casually
- the minimal shape of `IKaleidoBuilder`
- `KaleidoBuilder` assembly deduplication behavior
- the default no-op event publisher
- `DataTypeMapper` scalar/format conventions
- `ConstraintMapper` constraint naming and parameter conventions
- Queryable registration invariants (interface identity, type-name naming, one capability per type)
- `QuerySourceKind` staying unpublished (consumers see every source the same way)
- Process step registration invariants
- `ProcessorContext` as current resumable state only
- `Kaleido.Http.Abstractions` contract shapes without coordinating server and client changes

---

## Common pitfalls
- Moving HTTP or transport concerns into `Kaleido` (core) — `KaleidoCorrelationContext` is transport-agnostic; `KaleidoCorrelationHeaders` and `HttpHeaderSanitizer` belong in `Kaleido.Http.Abstractions`
- Moving runtime logic into `Kaleido.Http` (endpoint mapping and middleware only)
- Treating `AddAssembly(...)` as if it should also scan and register features
- Changing `Kaleido.Http.Abstractions` types without updating both the server endpoint and client
- Importing Queryable concepts into Process code or vice versa
- Adding a future transport (e.g. gRPC) as a dependency of `Kaleido.Http` — each transport is its own project (`Kaleido.Grpc`, etc.) with its own header/metadata constants

---

## Verification

Build all source projects:
```
dotnet build src/Kaleido/Kaleido.csproj
dotnet build src/Kaleido.Http/Kaleido.Http.csproj
dotnet build src/Kaleido.Http.Abstractions/Kaleido.Http.Abstractions.csproj
dotnet build src/Kaleido.Http.Client/Kaleido.Http.Client.csproj
dotnet build src/Kaleido.Provider.SQLite/Kaleido.Provider.SQLite.csproj
```

Or build the solution:
```
dotnet build Kaleido.slnx
```

Run tests — the `--` is required to trigger MTP mode (without it dotnet uses VSTest and finds zero tests):
```
dotnet test Kaleido.slnx --
```

If tests report zero, run `full_clean.cmd` from the repo root to clear stale bin/obj, then rebuild. See root `AGENTS.md` for details.
