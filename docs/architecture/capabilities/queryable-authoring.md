# Authoring Queryable sources and views

This is the consumer-side pattern for the [Queryable capability](queryable.md). The **source is the identity**; its query context describes how it is queried. The examples preserve the previously documented conventions while the broader #585 migration is underway; the [test/source analyzers](../../ANALYZERS.md) enforce the corresponding KAL rules.

```csharp
// The query context: how the source can be queried. A record with query rules, no identity.
public sealed record MemberQueryContext : IQueryContext
{
    [Key] public Guid MemberEnrollmentId { get; init; }

    [Searchable(Priority = 1, MatchMode = MatchMode.Exact)]
    [Sortable]
    public string MemberNumber { get; init; } = string.Empty;
}

// The source: the published capability. Queryable directly at /{service}/queryable/{source}/query.
[QuerySource(DisplayName = "Members", Description = "Member enrollments, searchable by member number and name.",
             DefaultSortField = nameof(MemberQueryContext.MemberNumber))]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class MemberQuerySource(MemberDbContext db) : IQuerySource<MemberQueryContext>
{
    public IQueryable<MemberQueryContext> CreateQuery(QueryExecutionContext ctx) =>
        db.MemberEnrollments.AsNoTracking().Select(e => new MemberQueryContext { /* ... */ });
}

// A view: a projection over one source, at /{service}/queryable/{source}/{view}/query.
[QueryView(DisplayName = "Member search", Description = "Searchable member results.",
           DefaultSortField = nameof(MemberQueryContext.MemberNumber))]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class MemberSearchView : IQueryViewSource<MemberQuerySource, MemberQueryContext, MemberSearchResult>
{
    public IQueryable<MemberSearchResult> CreateView(IQueryable<MemberQueryContext> query, QueryExecutionContext ctx) =>
        query.Select(x => new MemberSearchResult { /* ... */ });
}
```

## Declaration rules

- **Interfaces identify; attributes describe.** Discovery is interface-only: each source needs `[QuerySource]` and each view needs `[QueryView]` (KAL2010); an attribute without its interface is not discovered (KAL2009); both need nonempty `DisplayName` and `Description` (KAL2002/2003).
- **Public names are type names.** A source's `Type.Name` is unique per service; a view's is unique per source.
- Query contexts implement `IQueryContext` and declare `[Filterable]`, `[Searchable]`, `[Sortable]` on their properties; those attributes on other types have no effect (KAL2011). A context may back several sources. Parameter records implement `IQueryParameters`; view/result records are plain output shapes.
- A source's `[Pageable]` applies to its direct queries; a view's applies only to that view. Nothing is inherited. A pageable local source/view declares a sortable `DefaultSortField` (prefer a unique field), so an unsorted request has stable default ordering.
- Prefer `IQuerySource<T>` / `IQueryViewSource<…>` when the queryable is already available (in-memory, pre-fetched, deferred LINQ). Use async source/view interfaces when producing it requires async I/O. Never block inside a sync implementation with `.Result` or `.GetAwaiter().GetResult()`.
- A delegated source (`IDelegatedQuerySource<TQueryContext, TResult, TParameters>`) is a facade: Kaleido validates the consumer query against `TQueryContext`; the source translates it to a downstream query (including hidden internal criteria), calls a remote source/view using `query.ToApiBody()`, and maps its response. Paging/filtering/sorting occur downstream and `QueryResult` is returned unchanged. Delegated sources have no views; fulfillment kind is not published.

## Provider-native async execution

On a full page, `IQueryContextExecutor<TView>` may need to count matches. The default supports every `IQueryable`: it enumerates `IAsyncEnumerable<T>` to count or calls LINQ `Count()` synchronously. An async provider should register its own executor so a count uses a server-side async operation. Measured on EF Core with SQLite and a full page of 50 (`tools/benchmarks`, #326):

| Rows | Default executor | Provider executor (`SELECT COUNT`) |
|---|---|---|
| 1,000 | 0.66 ms, 395 KB | 0.13 ms, 84 KB |
| 10,000 | 5.9 ms, 3.2 MB | 0.37 ms, 84 KB |
| 100,000 | 55.9 ms, 31.3 MB | 3.0 ms, 84 KB |

The default grows with matching rows; the provider executor does not. An executor shared by EF and non-EF sources must choose EF operators only when `query.Provider is IAsyncQueryProvider`; EF async methods throw for an in-memory view beside EF sources (#352). MongoDB can apply the same principle with `CountDocumentsAsync`/`ToListAsync`.

```csharp
internal sealed class EfCoreQueryContextExecutor<TView> : IQueryContextExecutor<TView>
    where TView : class
{
    public Task<int> CountAsync(IQueryable<TView> query, CancellationToken cancellationToken) =>
        query.Provider is IAsyncQueryProvider       // Microsoft.EntityFrameworkCore.Query
            ? EntityFrameworkQueryableExtensions.CountAsync(query, cancellationToken)
            : Task.FromResult(query.Count());

    public async Task<IReadOnlyList<TView>> ToListAsync(IQueryable<TView> query, CancellationToken cancellationToken) =>
        query.Provider is IAsyncQueryProvider
            ? await EntityFrameworkQueryableExtensions.ToListAsync(query, cancellationToken)
            : query.ToList();

    public IQueryable<TView> ApplyPage(IQueryable<TView> query, int size, int offset) =>
        query.Skip(offset).Take(size);
}

builder.Services.AddSingleton(typeof(IQueryContextExecutor<>), typeof(EfCoreQueryContextExecutor<>));
```

The framework uses `TryAdd` for its default, so a host's registration wins before or after `AddQueryable()`. The integration example is `tests/Kaleido.IntegrationTests/EfCore/EfCoreQueryContextExecutorTests.cs`.
