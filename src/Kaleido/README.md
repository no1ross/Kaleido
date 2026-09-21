# Kaleido

This project is Kaleido's core runtime. It contains everything needed to bootstrap the framework and run Process and Queryable capabilities at the application layer, without requiring any transport or ASP.NET Core dependency.

For the full repository model, see:
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AGENTS.md`](../AGENTS.md)

---

## What lives here

### Bootstrap
- `KaleidoServiceCollectionExtensions` — `AddKaleido()` entry point and `AddAssembly(...)` helper
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
- `QueryableServiceCollectionExtensions` — `AddQueryable()` entry point
- `QueryableService` — main dispatch service (direct context → local view → delegated view)
- `IQueryContextRegistry` / `IQueryViewRegistry` / `IDelegatedQueryViewRegistry` — runtime registries
- `QueryContextEngine` / `QueryContextExecutor` — query execution pipeline
- `QueryRequestCompiler` / `QueryRequestValidator` — validation and compilation
- `QueryableBuilder` / `QueryableObservability` — builder and observability
- `IQueryContextSource<T>` / `IQueryContextSourceAsync<T>` / `IQueryViewSource` / `IQueryViewSourceAsync` / `IDelegateQueryViewSource` — source/view interfaces

### Process runtime
- `ProcessorServiceCollectionExtensions` — `AddProcessor(...)` entry point
- `ExecutionProcessor` — main step execution loop
- `StepCandidateBuilder` / `StepCandidateValidator` / `StepCandidateConsistencyChecker` / `StepCandidatePlanner` — planning pipeline
- `ProcessStepRegistry` / `ProcessorRegistry` — runtime registries
- `ProcessObservability` — observability
- `IProcessStepHandler<TStep>` / `IProcessStepHandler<TStep, TResult>` — handler contracts
- `IProcessContextStore` / `InMemoryProcessContextStore` — state store abstraction and default implementation

---

## What this project is for

Reference this project when you need to:
- bootstrap Kaleido with `AddKaleido()`
- register assemblies with `AddAssembly(...)`
- call `AddQueryable()` to enable the Queryable runtime
- call `AddProcessor(...)` to enable the Process runtime
- work with shared metadata primitives (`DataTypeMapper`, `ConstraintMapper`)
- implement a query context source, view source, or step handler
- work with correlation context or event publishing

## What this project is NOT for

This project does not contain:
- HTTP endpoint publication (see [`Kaleido.Http`](../Kaleido.Http/README.md))
- ASP.NET Core DI registration or transport services (see [`Kaleido.AspNetCore`](../Kaleido.AspNetCore/README.md))
- HTTP contract types (see [`Kaleido.Http.Abstractions`](../Kaleido.Http.Abstractions/README.md))
- Remote HTTP client consumption (see [`Kaleido.Http.Client`](../Kaleido.Http.Client/README.md))
- SQLite state persistence (see [`Kaleido.Provider.SQLite`](../Kaleido.Provider.SQLite/README.md))

---

## Bootstrap model

A service starts by calling `AddKaleido()`:

```csharp
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(MyDbContext).Assembly)
    .AddQueryable()
    .AddProcessor(options =>
    {
        options.Name = "my-processor";
        options.DisplayName = "My Processor";
        options.Version = "1.0.0";
    });
```

`AddKaleido()`:
- validates the `IServiceCollection`
- registers the scoped correlation accessor (using `TryAddScoped` so a pre-existing registration wins)
- registers the default no-op event publisher
- returns an `IKaleidoBuilder`

`AddAssembly(...)` records assemblies on the builder. Those assemblies become the shared scanning input for `AddQueryable()` and `AddProcessor(...)`.

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
3. register assemblies and call `AddProcessor(...)`
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
