# Kaleido source contributor guide

This is the contributor guide for `src/`. Read it before making changes to any Kaleido source project.

See also:
- [`ARCHITECTURE.md`](./ARCHITECTURE.md) — source-level architecture details
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — repo-level architecture overview
- [`../AGENTS.md`](../AGENTS.md) — repo-level contributor guide

---

## Scope and boundaries

What each project owns and must not own is stated once, in the root [project boundaries](../AGENTS.md#project-boundaries); each project's README lists its public types, and [`ARCHITECTURE.md`](./ARCHITECTURE.md) its internals. Two rules apply across `src/`:

- **Project references point one way.** A new reference is an architecture change:
  - `Kaleido` references no other Kaleido project. Its one tooling reference, to the consumer analyzer (`tools/analyzers/Kaleido.Analyzers`), carries only analyzer assets: it makes `Kaleido.nupkg` depend on the `Kaleido.Analyzers` package and neither compiles against nor runs the analyzer.
  - `Kaleido.Http.Abstractions`, `Kaleido.Observability.OpenTelemetry` and `Kaleido.Provider.SQLite` reference only `Kaleido`.
  - `Kaleido.Http` and `Kaleido.Http.Client` reference `Kaleido.Http.Abstractions`.
  - `Kaleido.Http.Mcp` references `Kaleido.Http` and `Kaleido.Http.Client`.
- **Public means consumer API.** Transports, front doors and providers are built into the solution, so seams shared between Kaleido's own projects are internal and shared through `InternalsVisibleTo`. A type is public only when a consumer must name, implement or call it, and then it carries full XML documentation (CS1591 is enforced). In particular, the runtime registries (`IProcessorStepRegistry`, `ProcessStepRegistration`, `IQuerySourceRegistry`, `IQueryViewRegistry`) and the in-process execution entry points (`IProcessorRuntime` with `ProcessRequest`/`ProcessorRequest`/`ProcessResult`, `IQueryableService`) are internal: only the transports use them (#297).

---

## Core runtime rules

### Bootstrap rules
`AddKaleido()` should remain responsible for:
- shared DI baseline setup
- scoped correlation accessor registration (`TryAddScoped` so pre-existing registrations win)
- default event publisher registration
- returning the builder for higher-level frameworks

`AddKaleido()` also registers both runtimes (the internal `AddProcessor()` and `AddQueryable()`), which scan the assemblies in `KaleidoServiceOptions.Assemblies`; the registration model is described once, in [`../ARCHITECTURE.md`](../ARCHITECTURE.md#4-registration-model).

### Builder rules
`IKaleidoBuilder` is intentionally small: `Services`, `Assemblies`, `Configuration` and `ServiceOptions` — what every subsystem builder (Process, Queryable, Registry, transports) needs without a DI lookup. Be cautious about expanding it — additional members affect every framework built on top.

`KaleidoBuilder` deduplicates assemblies by identity. Preserve that behavior.

### Shared metadata rules
`TypeDescriber` and `ConstraintMapper` are high-impact shared primitives. Changes ripple into anything that reflects CLR types into discovery metadata, projects validation rules, or converts transport values.

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
- **Paging is per capability:** the source's `[Pageable]` applies to direct queries, a view's to that view; it is never inherited. A pageable view, and a pageable local source, must declare a sortable `DefaultSortField`; a request without a sort is ordered by it (ascending), so pages are stable. A delegated source's order is its downstream's.

### Process runtime rules
Process is step-centric, not query-centric. Do not import Queryable terminology into Process code or docs.

Registration invariants:
- `AddKaleido()` must be called before `AddProcessor(...)`
- A step is identified by `IProcessStep`; `[ProcessStep]` only describes it. Every `IProcessStep` type needs `[ProcessStep]` with a non-empty `Version`, `DisplayName`, and `Description`, and `[ProcessStep]` is only valid on `IProcessStep` types
- A step's name is its type name (`Type.Name`, unmodified); step type names must be unique across the assembly scan
- Inside the runtime the next step is typed (`Success<TNext>()`, `Type` through invoker → evaluator); step names are produced only at the boundary (state, events, responses)
- Every discovered step must have exactly one handler
- No relationship may self-reference, and the `DependsOn` graph must not be circular; a relationship must name a step the processor registers (all fail at startup with `pro_invalid_registration`). `AvailableAfter`/`AvailableUntil` cycles are not detected

The planning pipeline is layered — keep those responsibilities separated:
- `StepCandidateBuilder` → resolves step names, hydrates input
- `StepCandidateValidator` → enforces `DataAnnotations`
- `StepCandidateConsistencyChecker` → enforces history, dependency, and repeatability consistency
- `StepCandidatePlanner` → orders executable candidates
- `StepCandidateNextStepChecker` → admits only next steps (the pending required step, or a step available under the process rules) and checks information-step answers against the pending request
- `ProcessorExecutor` → invokes handlers, evaluates decisions, updates state, persists, emits events

`ProcessorContext` is current resumable state only. Keep it small. Historical evidence belongs in emitted process events. The pending information request (`RequiredInformationRequest`) is part of that state; past questions and answers are not.

Information requests: Kaleido owns the schema, never the content. Validation is structural only; an `IInformationStep` declares only `InformationRequestId` and `Items`; a request travels only with the required step. See [`docs/INFORMATION_REQUESTS.md`](../docs/INFORMATION_REQUESTS.md) and ADRs 0002–0006.

---

### Telemetry conventions
Kaleido emits telemetry only for what only Kaleido can observe: the Process and Queryable runtimes,
the event outbox and relay, store providers and the MCP adapter. Host and transport statistics
(request rates, status codes, latencies) come from the host's own instrumentation - do not add a
Kaleido metric that duplicates them (ADR 0014).

Instrument names and tag keys live in the `*Telemetry` constants classes (`ProcessorTelemetry`,
`QueryableTelemetry`, `KaleidoEventTelemetry`, `SqliteTelemetry`, `McpTelemetry`) - constants only,
no `Meter`/`Counter` instances. Each emitting class owns its instruments as
`private static readonly Meter`/`Counter`/`Histogram` fields created with the shared `MeterName`;
when two classes emit on the same meter (e.g. the event outbox and the relay), each declares its own
instruments and the meter-name subscription merges them.

`Kaleido.Observability.OpenTelemetry` references only core: names of optional projects are copied
there as internal constants, and its unit tests assert each copy equals the owning constant. A new
meter or activity source is registered in `AddKaleidoInstrumentation()` in the same change.
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

`QueryResult<T>.TotalCount` semantics: always the number of matching results, whatever page was returned. A pageable source or view always pages (its default page when the request has no `page`). `CountAsync` runs only when the page cannot tell: the page is full, or empty past the start. A partial page ends the results (`TotalCount = offset + results.Count`), and an unpageable capability returns every result (`TotalCount = results.Count`). A delegated source's `TotalCount` is its downstream's.

When a delegated query source (`IDelegatedQuerySource`) receives a `QueryBody` and needs to forward it over HTTP, use `query.ToApiBody()` from `Kaleido.Http.Abstractions` — samples and consumers should only reference `Kaleido.Http.Abstractions`, not `Kaleido.Http` (which carries ASP.NET Core dependencies).

---

## Correlation rules
`KaleidoCorrelationContext` fields are transport-agnostic — they live in `Kaleido` core. The HTTP wire names (`KaleidoCorrelationHeaders`) live in `Kaleido.Http.Abstractions`. Changes to either affect transport handling, observability, event payloads, and higher-level runtime context propagation. Treat them as broad-impact changes.

---

## Eventing rules
`IEventPublisher` is intentionally infrastructure-agnostic. The default no-op publisher (internal `EventPublisher`, which logs a Warning that events are discarded) makes eventing optional. Do not remove that default.

---

## What not to change casually
- the minimal shape of `IKaleidoBuilder`
- `KaleidoBuilder` assembly deduplication behavior
- the default no-op event publisher
- `TypeDescriber` scalar/format conventions
- `ConstraintMapper` constraint naming and parameter conventions
- Queryable registration invariants (interface identity, type-name naming, one capability per type)
- `QuerySourceKind` staying unpublished (consumers see every source the same way)
- Process step registration invariants
- `ProcessorContext` as current resumable state only
- `Kaleido.Http.Abstractions` contract shapes without coordinating server and client changes

---

## Common pitfalls
- Moving HTTP or transport concerns into `Kaleido` (core) — `KaleidoCorrelationContext` is transport-agnostic; `KaleidoCorrelationHeaders` and `HttpHeaderSanitizerExtensions` belong in `Kaleido.Http.Abstractions`
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
dotnet build src/Kaleido.Http.Mcp/Kaleido.Http.Mcp.csproj
dotnet build src/Kaleido.Observability.OpenTelemetry/Kaleido.Observability.OpenTelemetry.csproj
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
