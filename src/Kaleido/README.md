# Kaleido

This project is Kaleido's core runtime. It contains everything needed to bootstrap the framework and run Process and Queryable capabilities at the application layer, without requiring any transport or ASP.NET Core dependency.

For the full repository model, see:
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AGENTS.md`](../AGENTS.md)

---

## What lives here

### Bootstrap
- `KaleidoServiceCollectionExtensions` — `AddKaleido()` entry point (assemblies supplied via `KaleidoServiceOptions.Assemblies`)
- `IKaleidoBuilder` / `KaleidoBuilder` — shared builder abstraction carrying `Services` and `Assemblies`
- `KaleidoCorrelationContextAccessor` — scoped correlation accessor registered by bootstrap
- `NullEventPublisher` — default no-op event publisher

### Shared abstractions
- `DataTypeMapper` — converts CLR types and properties into transport-friendly `DataTypeDescriptor` values
- `ConstraintMapper` — converts `ValidationAttribute` usage into metadata constraint contracts
- `KaleidoCorrelationContext` — shared ambient identity model for requests/workflows
- `IEventPublisher` / `IKaleidoEvent` — infrastructure-agnostic event publication seam
- `ValidationException` / `ValidationError` — shared error shape for validation failures
- `KaleidoEnumConverter` / `KaleidoEnumConverterFactory` — shared JSON enum conversion helpers
- `ValueConverter` — shared runtime value conversion helper

### Queryable runtime
- `QueryableServiceCollectionExtensions` — `AddQueryable()` (internal, auto-invoked by `AddKaleido()`)
- `QueryableService` — main dispatch service (by queried type: view, local source, or delegated source)
- `IQuerySourceRegistry` / `IQueryViewRegistry` — runtime registries (sources keyed by source type and name; views keyed by view type)
- `QueryContextEngine` / `DelegatedQuerySourceEngine` / `QueryContextExecutor` — query execution pipeline
- `IQueryContextExecutor<TView>` — public extension point: register your own implementation to plug in provider-native async execution (e.g. EF Core `CountAsync`/`ToListAsync`). Default executor uses `IAsyncEnumerable<T>` when supported, sync LINQ otherwise.
- `QueryRequestCompiler` / `QueryRequestValidator` — validation and compilation
- `QueryableBuilder` / `QueryableObservability` — builder and observability
- `IQuerySource<T>` / `IQuerySourceAsync<T>` / `IDelegatedQuerySource<…>` — source interfaces (the identity, described by `[QuerySource]`)
- `IQueryViewSource<TSource, …>` / `IQueryViewSourceAsync<TSource, …>` — view interfaces (the identity, described by `[QueryView]`)
- `IQueryContext` / `IQueryParameters` — markers for query context records (query rules) and parameter records

### Process runtime
- `ProcessorServiceCollectionExtensions` — `AddProcessor(...)` (internal, auto-invoked by `AddKaleido()`)
- `ExecutionProcessor` — main step execution loop
- `StepCandidateBuilder` / `StepCandidateValidator` / `StepCandidateConsistencyChecker` / `StepCandidatePlanner` — planning pipeline
- `ProcessorStepRegistry` / `ProcessorRegistry` — runtime registries
- `ProcessorObservability` — observability
- `IProcessStepHandler<TStep>` / `IProcessStepHandler<TStep, TResult>` — handler contracts
- `IProcessorContextStore` / `ProcessorContextStore` — state store abstraction and default implementation

---

## What this project is for

Reference this project when you need to:
- bootstrap Kaleido with `AddKaleido()` (which auto-registers the Queryable and Process runtimes)
- register assemblies via `KaleidoServiceOptions.Assemblies` in the `AddKaleido()` configure callback
- work with shared metadata primitives (`DataTypeMapper`, `ConstraintMapper`)
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
    o.ServiceName = "my-service";
    o.Assemblies = new[]
    {
        typeof(Program).Assembly,
        typeof(MyDbContext).Assembly
    };
});
```

`AddKaleido()`:
- validates the `IServiceCollection`
- registers the scoped correlation accessor (using `TryAddScoped` so a pre-existing registration wins)
- registers the default no-op event publisher
- automatically invokes `AddQueryable()` and `AddProcessor()` to register both runtimes
- returns an `IKaleidoBuilder`

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
- **How a source is fulfilled (`QuerySourceKind`: local or delegated) is runtime metadata for the service only.** It is available via `QueryExecutionContext.Metadata` and never published to consumers.

### Performance implications

- **Delegated sources pay full materialization cost:** the source computes and returns the whole `QueryResult<TResult>` (typically a cross-service call). Nothing re-queries it.
- **Local views pay projection cost only:** the framework applies request semantics (filter/sort/page) to an `IQueryable<TQueryContext>` in-process; paging bounds memory.
- **Direct source queries are the rawest lane:** request semantics apply directly against the source's queryable and return query context records.

---

## Process execution model

Process is step-centric:
1. declare a step type that implements `IProcessStep` (its identity) and carries `[ProcessStep]` (its required metadata: `Version`, `DisplayName`, `Description`)
2. implement exactly one `IProcessStepHandler<TStep>` (or `<TStep, TResult>`)
3. register the assemblies containing steps via `KaleidoServiceOptions.Assemblies`
4. let the runtime build a registry and manage state
5. submit one or more steps through `IProcessorRuntime` or the HTTP transport layer

A step's public name is its type name (`Type.Name`, unmodified) — in the registry, on the wire, in persisted state, and in events. Handlers name the next step by type (`ProcessStepHandlerResult.Success<TNext>()`); names are only produced at the boundary.

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
- `IKaleidoBuilder.cs` / `KaleidoBuilder.cs` — builder contract and implementation
- `DataTypeMapper.cs` / `ConstraintMapper.cs` — shared metadata utilities
- `Queryable/QueryableService.cs` — Queryable dispatch
- `Process/Execution/ExecutionProcessor.cs` — Process execution loop
- `Process/Planning/` — planning pipeline components
