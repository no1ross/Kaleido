# Kaleido

This project is Kaleido's core runtime. It contains everything needed to bootstrap the framework and run Process and Queryable capabilities at the application layer, without requiring any transport or ASP.NET Core dependency.

For the full repository model, see:
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AGENTS.md`](../AGENTS.md)

---

## What lives here

### Bootstrap
- `KaleidoServiceCollectionExtensions` — `AddKaleido()` entry point (assemblies supplied via `KaleidoServiceOptions.Assemblies`), `AddEventPublisher<T>()`, `AddEventRedactor<T>()`
- `IKaleidoBuilder` / `KaleidoBuilder` — shared builder abstraction carrying `Services`, `Assemblies`, `Configuration` and `ServiceOptions`
- `IKaleidoCorrelationContextAccessor` — scoped correlation accessor registered by bootstrap (a pre-existing registration wins)
- a default no-op `IEventPublisher` (internal) — events are discarded, with a Warning log, until the host calls `AddEventPublisher<T>()`

### Shared abstractions
- `ITypeDescriber` / `TypeDescriber` (internal) — converts CLR types and properties into `DataTypeDescriptor` values (internal; the HTTP transport maps them to the wire's `DataTypeMetadata`)
- `IConstraintMapper` / `ConstraintMapper` (internal) — converts `ValidationAttribute` usage into metadata constraint contracts
- `KaleidoCorrelationContext` — shared ambient identity model for requests/workflows
- `IEventPublisher` / `IKaleidoEvent` - infrastructure-agnostic event publication seam; events reach the publisher at least once through the outbox and the event relay (`KaleidoEventRelay`, `KaleidoEventOptions`, ADR 0012)
- `IKaleidoEventRedactor` / `AddEventRedactor<T>()` - reduce events before the outbox write; properties marked with a `DataClassificationAttribute` are redacted with the host's `AddRedaction` redactors, or erased when none is registered (ADR 0013)
- `Kaleido.Exceptions` — `KaleidoValidationException` (400), `KaleidoAuthorizationException` (401/403), `KaleidoConcurrencyException` (409), `KaleidoConfigurationException` and `KaleidoFrameworkException` (500); the HTTP transport maps them to the error body `KaleidoErrorResponse`, which lives in `Kaleido.Http.Abstractions` (see [`docs/ERROR_CODES.md`](../../docs/ERROR_CODES.md), ADR 0017)

### Queryable runtime
- `QueryableServiceCollectionExtensions` — `AddQueryable()` (internal, auto-invoked by `AddKaleido()`)
- `IQueryableService` / `QueryableService` (internal; called by the HTTP transport) — main dispatch service (by queried type: view, local source, or delegated source)
- `IQuerySourceRegistry` / `IQueryViewRegistry` (internal) — runtime registries (sources keyed by source type and name; views keyed by view type)
- `QueryContextEngine` / `DelegatedQuerySourceEngine` / `QueryContextExecutor` — query execution pipeline
- `IQueryContextExecutor<TView>` — public extension point: register your own implementation to plug in provider-native async execution (e.g. EF Core `CountAsync`/`ToListAsync`, MongoDB `CountDocumentsAsync`). The default knows no provider: it uses `IAsyncEnumerable<T>` when supported, sync LINQ otherwise, and counts an async query by reading every matching row, so a host on an async provider registers that provider's executor ([docs/PATTERNS.md](../../docs/PATTERNS.md#queryable-sources-views-and-query-contexts)).
- `QueryRequestCompiler` / `QueryRequestValidator` — validation and compilation
- `QueryableObservability` (internal) — logs, traces and metrics of the Queryable runtime
- `IQuerySource<T>` / `IQuerySourceAsync<T>` / `IDelegatedQuerySource<…>` — source interfaces (the identity, described by `[QuerySource]`)
- `IQueryViewSource<TSource, …>` / `IQueryViewSourceAsync<TSource, …>` — view interfaces (the identity, described by `[QueryView]`)
- `IQueryContext` / `IQueryParameters` — markers for query context records (query rules) and parameter records

### Process runtime
- `ProcessorServiceCollectionExtensions` — `AddProcessor(...)` (internal, auto-invoked by `AddKaleido()`)
- `IProcessorRuntime` (internal) — runs submitted steps in-process (what the HTTP transport calls); `ProcessorExecutor` (internal) is the main step execution loop
- `StepCandidateBuilder` / `StepCandidateValidator` / `StepCandidateConsistencyChecker` / `StepCandidatePlanner` / `StepCandidateNextStepChecker` — planning pipeline
- `IProcessorStepRegistry` / `IProcessorRegistry` (internal, shared with `Kaleido.Http`) — runtime registries
- `ProcessorObservability` (internal) — logs, traces and metrics of the runtime
- `IProcessStepHandler<TStep>` / `IProcessStepHandler<TStep, TResult>` — handler contracts
- `IInformationStep` / `InformationRequest` / `RequireInformation<TNext>` — information requests: runtime-decided questions answered by an information step (see [`docs/INFORMATION_REQUESTS.md`](../../docs/INFORMATION_REQUESTS.md))
- `IKaleidoContextStore` — the process-state store; with `IKaleidoOutboxStore` (the event outbox, in the same database: a save's events are written in the state's transaction) and `IKaleidoIdempotencyStore` (requests sent with an idempotency key) one of three store contracts, one concern each (ADR 0020). The in-memory defaults (internal) are for development; each logs a Warning that its data is lost on restart

---

## What this project is for

Reference this project when you need to:
- bootstrap Kaleido with `AddKaleido()` (which auto-registers the Queryable and Process runtimes)
- register assemblies via `KaleidoServiceOptions.Assemblies` in the `AddKaleido()` configure callback
- work with shared metadata primitives (`TypeDescriber`, `ConstraintMapper`)
- implement a query source, query view, delegated query source, or step handler
- work with correlation context or event publishing

## What this project is NOT for

This project does not contain:
- HTTP endpoint publication, DI registration, middleware, or transport services (see [`Kaleido.Http`](../Kaleido.Http/README.md))
- HTTP contract types (see [`Kaleido.Http.Abstractions`](../Kaleido.Http.Abstractions/README.md))
- Remote HTTP client consumption (see [`Kaleido.Http.Client`](../Kaleido.Http.Client/README.md))
- SQLite state persistence (see [`Kaleido.Provider.SQLite`](../Kaleido.Provider.SQLite/README.md))

---

## Bootstrap model

A service starts by calling `AddKaleido()`:

```csharp
builder.Services.AddKaleido(builder.Configuration, o =>
{
    o.ServiceName = "myservice"; // [a-z0-9][a-z0-9_-]*: it is the route prefix and the MCP tool prefix (KAL2005)
    o.Assemblies =
    [
        typeof(Program).Assembly,
        typeof(MyDbContext).Assembly
    ];
});
```

Other `KaleidoServiceOptions` (bound from the `Kaleido` configuration section, then the callback):
- `DisplayName` / `Description` — the service's human-readable name and description, published in the registry
- `IsEntryProcessor` — marks this service's processor as the entry point of an aggregated registry (only one may)
- `InstanceId` — identifies this running instance (new at each start by default) in its own trace tags, events and logs; never sent or read in correlation headers
- `TypeFilter` — limits which step and query source/view types are registered when an assembly is shared
- `AuthorizationMode` / `DefaultAuthorization` — capability authorization ([`docs/AUTHORIZATION.md`](../../docs/AUTHORIZATION.md))
- `Events` — the event relay (`Kaleido:Events`, `KaleidoEventOptions`)

`AddKaleido()`:
- validates the `IServiceCollection`
- registers the scoped correlation accessor (using `TryAddScoped` so a pre-existing registration wins)
- registers the default no-op event publisher, the three in-memory stores (`IKaleidoContextStore`, `IKaleidoOutboxStore`, `IKaleidoIdempotencyStore`) and the event relay (a hosted background service that hands outbox entries to the publisher - see `KaleidoEventOptions`, `Kaleido:Events`)
- automatically invokes `AddQueryable()` and `AddProcessor()` to register both runtimes
- returns an `IKaleidoBuilder`

`AddKaleido()` is called once per service collection: a host runs one Kaleido service. A second call throws `KaleidoConfigurationException` (`duplicate_registration`) instead of silently replacing the first one's options; the same holds for `AddHttp()` (#301).

`KaleidoServiceOptions.Assemblies` records the assemblies that become the shared scanning input for the Queryable and Process runtimes. A non-empty list is required: `AddKaleido()` throws `KaleidoConfigurationException` (`missing_assembly`) before changing DI when it is omitted or empty. There is no calling-assembly or entry-assembly fallback. Even a client-only host must choose a scan set deliberately; it can use `typeof(KaleidoServiceOptions).Assembly` when it does not publish application capabilities.

---

## Queryable model and execution

**The source is the identity; the query context describes how it is queried.** See [`docs/PATTERNS.md`](../../docs/PATTERNS.md) for the canonical shapes.

| Capability | Identity (interface) | Metadata | Public name / route | Who applies search, filter, sort, paging |
|---|---|---|---|---|
| **Local source** | `IQuerySource<TQueryContext>` / `IQuerySourceAsync<TQueryContext>` | `[QuerySource]` | `Type.Name` (unique per service), `/{source}/query` | Kaleido, in-process |
| **Local view** | `IQueryViewSource<TSource, TQueryContext, TView[, TParams]>` / async | `[QueryView]` | `Type.Name` (unique per source), `/{source}/{view}/query` | Kaleido (on the source's queryable), then the view's projection |
| **Delegated source** | `IDelegatedQuerySource<TQueryContext, TResult[, TParams]>` | `[QuerySource]` | `Type.Name`, `/{source}/query` | the downstream system; Kaleido passes the `QueryResult` through untouched |

- **Discovery is interface-only:** a source or view must carry its attribute, with a non-empty `Version`, `DisplayName` and `Description`.
- **A type is exactly one capability,** so `QueryableService.QueryAsync<TQuery, TResult>` dispatches by the queried type. A view type runs the view over its source; a local source type runs a direct query; a delegated source type runs the delegated engine. There are no competing lanes and no fallback.
- **Every local source can be queried directly.** Exposure is controlled by what its query context publishes and by authorization.
- **A query context may back several sources.** Sources are registered and resolved by their concrete type.
- **The consumer's query is always validated against the source's query context,** for delegated sources too. Paging comes from the source's `[Pageable]` (direct queries) or the view's (view queries); it is not inherited.
- **Pages are stable.** A pageable view or local source declares a sortable `DefaultSortField` (on `[QueryView]` / `[QuerySource]`; startup fails with `qry_invalid_registration` otherwise), and a request without a sort is ordered by it, ascending. Prefer a unique field: rows with equal values can still swap places between pages. A delegated source's order is its downstream's. The default is published as `pageable.defaultSortField`.
- **Query parameters are validated before the query runs,** like a step's input: the parameters record's DataAnnotations (`[Required]` → `qry_missing_parameter`, others → `qry_invalid_parameter_value`), and a missing record is rejected when it has a `[Required]` member. A source or view can rely on its required parameters being present. For value types, declare the parameter with the C# `required` keyword (or a nullable type): on a plain `Guid` an omitted value is `Guid.Empty` and passes `[Required]` (KAL2022). The same holds for step inputs (KAL2021).
- **Request bodies are read by Kaleido.** The per-step and query endpoints deserialize their typed body themselves (`KaleidoJsonBody<T>`), so a body that does not fit (a malformed value, a `required` member left out) is answered 400 `argument_error` naming the JSON path, by Kaleido's error handling - never by the host's developer exception page or `UseExceptionHandler`.
- **How a source is fulfilled (`QuerySourceKind`: local or delegated) is runtime metadata for the service only.** It is internal: never published, and not passed to sources and views (`QueryExecutionContext` carries only the request).

### Performance implications

- **Delegated sources pay full materialization cost:** the source computes and returns the whole `QueryResult<TResult>` (typically a cross-service call). Nothing re-queries it.
- **Local views pay projection cost only:** the framework applies request semantics (filter/sort/page) to an `IQueryable<TQueryContext>` in-process; paging bounds memory.
- **Direct source queries are the rawest lane:** request semantics apply directly against the source's queryable and return query context records.

---

## Process execution model

Process is step-centric:
1. declare a step type that implements `IProcessStep` (its identity) and carries `[ProcessStep]` (its required metadata: `Version`, `DisplayName`, `Description`) and, where it applies, what it does in the world: `Destructive`, `External`, `RequiresConfirmation` (ADR 0015)
2. implement exactly one `IProcessStepHandler<TStep>` (or `<TStep, TResult>`)
3. register the assemblies containing steps via `KaleidoServiceOptions.Assemblies`
4. let the runtime build a registry and manage state
5. submit one or more steps through a transport (the HTTP transport, `Kaleido.Http`); the in-process runtime it calls is internal

A step's public name is its type name (`Type.Name`, unmodified) — in the registry, on the wire, in persisted state, and in events. Handlers name the next step by type (`ProcessStepHandlerResult.Success<TNext>()`, or `ProcessStepHandlerResult.Success(response).Next<TNext>()` with a typed response); names are only produced at the boundary.

The runtime:
- builds and validates submitted step candidates
- evaluates dependency and repeatability rules (`[DependsOn<T>]`, `[AvailableAfter<T>]`, `[AvailableUntil<T>]`, `[Repeatable]`)
- orders executable steps
- invokes handlers
- persists updated processor state
- returns step results plus next-step guidance

---

## Where to look

- `KaleidoServiceCollectionExtensions.cs` — bootstrap entry point
- `KaleidoBuilder.cs` — builder contract (`IKaleidoBuilder`) and implementation
- `KaleidoServiceOptions.cs` — service options and their validation
- `TypeDescriber.cs` / `ConstraintMapper.cs` — shared metadata utilities
- `Queryable/QueryableService.cs` — Queryable dispatch
- `Processor/Execution/ProcessorExecutor.cs` — Process execution loop
- `Processor/Planning/` — planning pipeline components
