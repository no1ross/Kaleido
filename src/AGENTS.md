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
- root bootstrap (`AddKaleido()`, `IKaleidoBuilder`, `AddAssembly(...)`)
- shared metadata primitives (`DataTypeMapper`, `ConstraintMapper`)
- shared eventing abstractions and correlation context
- shared JSON/value-conversion helpers
- Queryable runtime: context/view registration, validation, dispatch, execution, observability
- Process runtime: step registration, planning, execution, state mutation, observability
- Default in-memory `IProcessContextStore`

Does **not** own HTTP endpoints, ASP.NET Core DI, HTTP contracts, remote client consumption, or SQLite persistence.

### `src/Kaleido.Http`
Owns the full HTTP transport layer — middleware, correlation propagation, and endpoint publication:
- `AddHttp()` — public entry point; registers routing, `IHttpContextAccessor`, the middleware pipeline, and HTTP execution services
- `ExceptionMiddleware` — outermost middleware; maps exceptions to JSON error responses
- `ObservabilityMiddleware` — reads inbound correlation headers via `HttpCorrelationContextReader`, populates `IKaleidoCorrelationContextAccessor`, tags the `Activity`, and echoes the full correlation context on the response
- `HttpCorrelationContextReader` — reads and sanitizes inbound HTTP headers into `KaleidoCorrelationContext`
- `KaleidoStartupFilter` — registers middlewares via `IStartupFilter` in the correct pipeline order
- `MapQueryable()` — all Queryable HTTP endpoints
- `MapProcessor()` — all Process HTTP endpoints
- `MapRegistry()` — aggregated discovery endpoint
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
- `IKaleidoProcessClientFactory` / `KaleidoProcessClient`
- `IKaleidoQueryableClientFactory` / `KaleidoQueryableClient`
- `AddHttpClients()` — consumer-facing registration (config-driven `Kaleido:Clients`); `AddProcessClient(...)`/`AddQueryableClient(...)` are internal

Does **not** own server-side runtime logic or endpoint mapping.

### `src/Kaleido.Observability.OpenTelemetry`
Owns the OpenTelemetry observability provider (opt-in, no OTel dependency in core):
- `AddOpenTelemetry()` on `IKaleidoBuilder` — one-call OTel setup: logging + tracing + metrics + OTLP export
- `AddKaleidoInstrumentation()` on `TracerProviderBuilder` — registers Kaleido `ActivitySource`s
- `AddKaleidoInstrumentation()` on `MeterProviderBuilder` — registers Kaleido `Meter`s with tuned histogram Views
- Does **not** own core instrumentation — `ActivitySource`/`Meter` definitions stay in `Kaleido`

Future observability providers follow the same pattern: `Kaleido.Observability.<Technology>`.

### `src/Kaleido.Provider.SQLite`
Owns the SQLite-backed durable process state store:
- `UseSqliteProcessContextStore(...)` extension
- SQLite-backed `IProcessContextStore` implementation

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
`IKaleidoBuilder` is intentionally small (`Services` + `Assemblies`). Be cautious about expanding it — additional members affect every framework built on top.

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
The dispatch order in `QueryableService` is fixed:
1. delegated view registry
2. local view registry
3. direct context registry fallback

Do not change that order. It is published framework semantics.

`QueryContextKind` semantics, pageable/default-sort validation behavior, and the context-centric discovery model must stay stable unless the semantics are intentionally changing.

### Process runtime rules
Process is step-centric, not query-centric. Do not import Queryable terminology into Process code or docs.

Registration invariants:
- `AddKaleido()` must be called before `AddProcessor(...)`
- Every `[ProcessStep]` must have a non-empty `Name` and `Version`
- Process step names must be unique across the assembly scan
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

## Transport and HTTP rules

### Kaleido.Http.Abstractions stability
This is a shared contract boundary. Treat it like a public API:
- prefer additive changes (new optional fields) over breaking changes
- when a contract must change, update the matching endpoint, client method, and tests together

### Kaleido.Http should stay thin
Middleware and endpoint mapping code should adapt contracts and wire the runtime. It should not reimplement runtime planning or execution logic that belongs in `Kaleido`.

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
- `QueryableService` dispatch order
- `QueryContextKind` semantics
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

Run tests:
```
dotnet test Kaleido.slnx
```
