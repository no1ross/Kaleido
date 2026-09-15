# Queryable

Queryable is Kaleido’s metadata-driven query framework for discoverable record search, filtering, sorting, and paging.

It is organized into four projects:

- [`Abstractions`](./Abstractions/README.md) — public contracts, metadata, attributes, request/result types, source/view interfaces, and shared Queryable options
- [`Queryable`](./Queryable/README.md) — runtime registration, validation, dispatch, execution, and observability
- `AspNetCore.Abstractions` — HTTP request/response contracts and transport-facing Queryable contract shapes
- [`AspNetCore`](./AspNetCore/README.md) — HTTP integration, endpoint publishing, request normalization, and ASP.NET Core runtime behavior

For the full subsystem model, see:
- [`ARCHITECTURE.md`](./ARCHITECTURE.md)
- [`AGENTS.md`](./AGENTS.md)

## Execution model

Queryable currently supports three execution lanes:

1. **Direct context**
   - query the context type itself
   - valid only for contexts marked `Direct`

2. **Local view**
   - named view over a local `IQueryable<TContext>`
   - implemented with `IQueryViewSource`

3. **Delegated view**
   - named async/orchestrated view
   - implemented with `IDelegateQueryViewSource`

Use:
- **direct context** when the context itself is the result shape
- **local view** when the result is a local projection over a queryable source
- **delegated view** when the backend must do async orchestration or hide internal downstream query logic from the caller

## Getting started

Queryable consumer setup follows a simple pattern:

1. define a query context type
2. implement an `IQueryContextSource<TContext>` if the context is locally queryable
3. define one or more query views over that context
4. register the assemblies that contain those types
5. call `AddQueryable()`
6. optionally call `AddQueryableAspNetCore(...)` and map endpoints with `MapQueryable()`

The runtime will:
- discover contexts and views
- build metadata registries
- validate requests
- execute direct, local-view, or delegated-view queries
- publish discovery metadata through ASP.NET Core if enabled

## How a developer uses Queryable

### 1. Register Queryable in your application
At minimum, register the assemblies that contain your Queryable types and then call `AddQueryable()`.

```csharp
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(MyDbContext).Assembly)
    .AddQueryable();
```

If you want HTTP discovery and query endpoints:

```csharp
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(MyDbContext).Assembly)
    .AddQueryable()
        .AddQueryableAspNetCore();

var app = builder.Build();
app.MapQueryable();
```

This matches the real registration pattern used in the samples. See <ref_snippet file="C:\Repos\Kaleido\samples\PriorAuth\ProviderSearch\Program.cs" lines="85-100" />.

### 2. Define a query context
A query context describes the searchable/filterable/sortable record shape for a dataset.

Use `[QueryContext]` on the type and decorate public properties with query semantics like `[Searchable]`, `[Filterable]`, and `[Sortable]`.

```csharp
[QueryContext(
    Name = "requesting-providers",
    DisplayName = "Requesting Providers",
    Version = "1.0.0",
    Source = "My Service")]
[Pageable(DefaultSize = 25, MaxSize = 250)]
public sealed class RequestingProviderQueryContext
{
    [Searchable(Priority = 1, MatchMode = MatchMode.Contains)]
    [Sortable]
    public string ProviderName { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    public Guid? PrimaryMedicalSpecialtyId { get; init; }
}
```

Real example: <ref_snippet file="C:\Repos\Kaleido\samples\PriorAuth\ProviderSearch.Artifacts\Queryable\Contexts\RequestingProviderQueryContext.cs" lines="7-57" />.

### 3. Implement a context source for local/direct contexts
If the data is locally queryable, implement `IQueryContextSource<TContext>`.
The framework will ask this source for the base `IQueryable<TContext>` and then apply Queryable’s filtering/search/sort/page behavior.

```csharp
internal sealed class RequestingProviderQueryContextSource(
    ProviderSearchDbContext dbContext)
    : IQueryContextSource<RequestingProviderQueryContext>
{
    public IQueryable<RequestingProviderQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.ProviderLocations
            .Select(x => new RequestingProviderQueryContext
            {
                ProviderName = x.Provider.ProviderName
            });
    }
}
```

#### Async context sources
If building the base `IQueryable<TContext>` requires an `await` — for example, fetching filter parameters from another Kaleido service before composing the query — implement `IQueryContextSourceAsync<TContext>` instead.
The framework awaits the returned task, then applies its normal search/filter/sort/page/materialization pipeline on the `IQueryable<TContext>`.

```csharp
internal sealed class RequestingProviderQueryContextSource(
    ProviderSearchDbContext dbContext,
    PlanNetworkClient planNetworkClient)
    : IQueryContextSourceAsync<RequestingProviderQueryContext>
{
    public async Task<IQueryable<RequestingProviderQueryContext>> CreateQueryAsync(
        QueryExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        var parameters =
            executionContext.TryGetViewParameters<RequestingProviderSearchParameters>()
            ?? throw new InvalidOperationException("Parameters are required.");

        // async call resolved before IQueryable is built
        var networkIds =
            (await planNetworkClient.GetNetworkIdsByPlanIdAsync(
                parameters.PlanId, cancellationToken))
            .ToArray();

        return dbContext.ProviderLocations
            .Where(x => dbContext.ProviderLocationNetworks
                .Any(n => n.ProviderLocationId == x.ProviderLocationId
                          && networkIds.Contains(n.NetworkId)))
            .Select(x => new RequestingProviderQueryContext
            {
                ProviderName = x.Provider.ProviderName
            });
    }
}
```

**Rules:**
- Register exactly one of `IQueryContextSource<T>` or `IQueryContextSourceAsync<T>` per context type — the framework rejects both or neither.
- The returned `IQueryable<TContext>` is not yet materialized. The framework applies search/filter/sort/page on top of it.
- Communicate with other Kaleido services using a client backed by `IKaleidoQueryableClientFactory` (or `IKaleidoProcessClientFactory`), never a raw `HttpClient`.

Real example: <ref_snippet file="C:\Repos\Kaleido\samples\PriorAuth\Provider.Artifacts\Queryable\ContextSources\RequestingProviderQueryContextSource.cs" lines="1-80" />.

### 4. Define a local query view
A local view is the normal way to expose a named result shape over a queryable context.
Implement `IQueryViewSource<TContext, TView>` or `IQueryViewSource<TContext, TView, TParameters>` and mark the class with `[QueryView]`.

```csharp
[QueryView(
    Name = "requesting-provider-search",
    DisplayName = "Requesting Provider Search",
    Version = "1.0.0",
    Description = "Search results for requesting providers.",
    DefaultSortField = nameof(RequestingProviderQueryContext.ProviderName))]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class RequestingProviderSearchViewSource
    : IQueryViewSource<
        RequestingProviderQueryContext,
        RequestingProviderSearchView,
        RequestingProviderSearchParameters>
{
    public IQueryable<RequestingProviderSearchView> CreateView(
        IQueryable<RequestingProviderQueryContext> query,
        QueryExecutionContext executionContext)
    {
        return query.Select(x => new RequestingProviderSearchView
        {
            ProviderName = x.ProviderName
        });
    }
}
```

Real example: <ref_snippet file="C:\Repos\Kaleido\samples\PriorAuth\Provider.Artifacts\Queryable\ViewSources\RequestingProviderSearchViewSource.cs" lines="1-44" />.

#### Async view sources
If projecting from `IQueryable<TContext>` to `IQueryable<TView>` requires an `await`, implement `IQueryViewSourceAsync<TContext, TView>` or `IQueryViewSourceAsync<TContext, TView, TParameters>`.

```csharp
internal sealed class MyViewSource
    : IQueryViewSourceAsync<MyQueryContext, MyView>
{
    public async Task<IQueryable<MyView>> CreateViewAsync(
        IQueryable<MyQueryContext> query,
        QueryExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        // async work before composing view...
        return query.Select(x => new MyView { ... });
    }
}
```

**Rules:**
- Implement exactly one of `IQueryViewSource` or `IQueryViewSourceAsync` per view type — the framework rejects both.
- Async view sources remain in the local-lane pipeline; the framework still owns materialization.

### 5. Define view parameters when needed
If a view needs typed inputs, add a parameter type and use the three-generic-argument form of `IQueryViewSource<...>`.
Public properties on the parameter type become documented parameter metadata, including validation constraints.

Example from the functional test models:
- parameter shape <ref_snippet file="C:\Repos\Kaleido\tests\Queryable\AspNetCore.FunctionalTests\Infrastructure\FunctionalSampleModels.cs" lines="112-117" />
- paired view <ref_snippet file="C:\Repos\Kaleido\tests\Queryable\AspNetCore.FunctionalTests\Infrastructure\FunctionalSampleModels.cs" lines="69-96" />

### 6. Understand view output shape limits
`TView` is the returned representation shape for a query result. That means a detail-style view can return a richer object graph than the query context itself, including nested objects or child collections such as notes.

Current behavior:
- complex nested `TView` output can work as a runtime payload
- ASP.NET Core can still serialize the returned `QueryResult<TView>` records normally
- registry output currently describes only the top-level `TView` properties in `OutputFields`
- complex nested output members are currently represented with a datatype of `object`

This is an important distinction:
- query context fields and parameter properties are part of Queryable's semantic query model
- `TView` output is the returned representation model

Future enhancement:
- recursively describe nested complex `TView` output metadata so detail views are more self-describing for clients and orchestrators

### 7. Choose the right execution lane
Use a **direct context** when the context itself is the result shape.
This requires `QueryContextKind.Direct` on the context metadata.

Use a **local view** when:
- the source data is locally queryable
- the view is a projection over `IQueryable<TContext>`
- the framework should own filtering, sorting, paging, and materialization
- use the `Async` variant (`IQueryContextSourceAsync` / `IQueryViewSourceAsync`) if setup requires `await`

Use a **delegated view** when:
- the backend must call another service and return results from it
- internal downstream query logic should stay hidden from the consumer
- the result is not locally queryable (pre-materialized results from another service)

Do **not** use a delegated view just to get `await`. If the data is still local, use the async source/view interfaces.

### 8. What Queryable registers for you
After `AddQueryable()` runs, the framework scans your registered assemblies and automatically discovers:
- `[QueryContext]` types
- `[QueryView]` types
- `IQueryContextSource<TContext>` and `IQueryContextSourceAsync<TContext>` implementations
- local and delegated query views (sync and async variants)

You do not manually register each context/view one by one. The assembly scan and registration flow is implemented in <ref_file file="C:\Repos\Kaleido\src\Queryable\Queryable\QueryableServiceCollectionExtensions.cs" />.

## Where to look

- Start with [`Abstractions/README.md`](./Abstractions/README.md) for public contracts
- Read [`Queryable/README.md`](./Queryable/README.md) for runtime behavior
- Read [`AspNetCore/README.md`](./AspNetCore/README.md) for HTTP/discovery behavior
- Read [`ARCHITECTURE.md`](./ARCHITECTURE.md) for the full architecture
- Read [`AGENTS.md`](./AGENTS.md) before making framework changes