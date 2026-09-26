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
- `QueryableService` — main dispatch service (delegated view → local view → direct context)
- `IQueryContextRegistry` / `IQueryViewRegistry` / `IDelegatedQueryViewRegistry` — runtime registries
- `QueryContextEngine` / `QueryContextExecutor` — query execution pipeline
- `IQueryContextExecutor<TView>` — public extension point: register your own implementation to plug in provider-native async execution (e.g. EF Core `CountAsync`/`ToListAsync`). Default executor uses `IAsyncEnumerable<T>` when supported, sync LINQ otherwise.
- `QueryRequestCompiler` / `QueryRequestValidator` — validation and compilation
- `QueryableBuilder` / `QueryableObservability` — builder and observability
- `IQueryContextSource<T>` / `IQueryContextSourceAsync<T>` / `IQueryViewSource` / `IQueryViewSourceAsync` / `IDelegateQueryViewSource` — source/view interfaces

### Process runtime
- `ProcessorServiceCollectionExtensions` — `AddProcessor(...)` (internal, auto-invoked by `AddKaleido()`)
- `ExecutionProcessor` — main step execution loop
- `StepCandidateBuilder` / `StepCandidateValidator` / `StepCandidateConsistencyChecker` / `StepCandidatePlanner` — planning pipeline
- `ProcessStepRegistry` / `ProcessorRegistry` — runtime registries
- `ProcessObservability` — observability
- `IProcessStepHandler<TStep>` / `IProcessStepHandler<TStep, TResult>` — handler contracts
- `IProcessContextStore` / `InMemoryProcessContextStore` — state store abstraction and default implementation

---

## What this project is for

Reference this project when you need to:
- bootstrap Kaleido with `AddKaleido()` (which auto-registers the Queryable and Process runtimes)
- register assemblies via `KaleidoServiceOptions.Assemblies` in the `AddKaleido()` configure callback
- work with shared metadata primitives (`DataTypeMapper`, `ConstraintMapper`)
- implement a query context source, view source, or step handler
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

`KaleidoServiceOptions.Assemblies` records the assemblies that become the shared scanning input for the Queryable and Process runtimes.

---

## Queryable execution lanes

Queryable supports three execution lanes, dispatched in this order:

1. **Delegated view** — async orchestration; returns a pre-materialized `QueryResult<TView>`. Implement `IDelegateQueryViewSource`.
2. **Local view** — projection over a local `IQueryable<TContext>`; framework applies search/filter/sort/page. Implement `IQueryViewSource` (or `IQueryViewSourceAsync` when setup requires `await`).
3. **Direct context** — query the context type itself directly. Valid only for contexts marked `Direct`.

Do not change that dispatch order. It is part of the current framework semantics.

---

## Process execution model

Process is step-centric:
1. declare a step type with `[ProcessStep]`
2. implement exactly one `IProcessStepHandler<TStep>` (or `<TStep, TResult>`)
3. register the assemblies containing steps via `KaleidoServiceOptions.Assemblies`
4. let the runtime build a registry and manage state
5. submit one or more steps through `IProcessorRuntime` or the HTTP transport layer

The runtime:
- builds and validates submitted step candidates
- evaluates dependency and repeatability rules (`AvailableAfter`, `DependsOnStep`, `Repeatable`)
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
