namespace Kaleido.Queryable;

/// <summary>
/// Marks a record as a <b>query context</b>: the record that describes how a query source can be
/// queried. Its properties carry the query rules — <see cref="FilterableAttribute"/>,
/// <see cref="SearchableAttribute"/>, <see cref="SortableAttribute"/> — and it is the record type a
/// local source produces and a direct query returns.
/// </summary>
/// <remarks>
/// <para>
/// A query context is not a capability on its own and has no public name; a source
/// (<see cref="IQuerySource{TQueryContext}"/>, <see cref="IQuerySourceAsync{TQueryContext}"/> or
/// <see cref="IDelegatedQuerySource{TQueryContext,TResult,TParameters}"/>) is. The same context may
/// be used by several sources.
/// </para>
/// <para>
/// The query-rule attributes only have meaning on query contexts; an analyzer reports them on
/// other types.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed record MemberQueryContext : IQueryContext
/// {
///     [Key] public Guid MemberEnrollmentId { get; init; }
///
///     [Searchable(Priority = 1, MatchMode = MatchMode.Exact)]
///     [Sortable]
///     public string MemberNumber { get; init; } = string.Empty;
/// }
/// </code>
/// </example>
public interface IQueryContext;

/// <summary>
/// Marks a record as the <b>parameters</b> of a query view or delegated source: named inputs
/// beyond search, filter, sort and paging (for example a process id). Parameters are published in
/// the registry with their validation constraints.
/// </summary>
/// <remarks>
/// Use <see cref="EmptyQueryViewParameters"/> (or the generic overloads without a parameters type
/// argument) when no parameters are needed.
/// </remarks>
public interface IQueryParameters;

/// <summary>Parameters record used when a query view or delegated source declares no parameters.</summary>
[ExcludeFromCodeCoverage]
public sealed record EmptyQueryViewParameters : IQueryParameters;

/// <summary>
/// Shared marker for the two local source shapes (<see cref="IQuerySource{TQueryContext}"/> and
/// <see cref="IQuerySourceAsync{TQueryContext}"/>). It lets local query views reference a source
/// without caring whether it is synchronous or asynchronous.
/// </summary>
/// <typeparam name="TQueryContext">The query context record the source produces.</typeparam>
/// <remarks>
/// Do not implement this interface directly; implement exactly one of
/// <see cref="IQuerySource{TQueryContext}"/> or <see cref="IQuerySourceAsync{TQueryContext}"/>.
/// Startup rejects a type that implements only this marker.
/// </remarks>
public interface ILocalQuerySource<TQueryContext>
    where TQueryContext : class, IQueryContext;

/// <summary>
/// A local query source: the published, queryable capability that supplies data shaped as
/// <typeparamref name="TQueryContext"/>. Implementing this interface is the source's
/// <b>identity</b>; describe it with <see cref="QuerySourceAttribute"/>.
/// </summary>
/// <typeparam name="TQueryContext">
/// The query context record. It describes how the source can be queried and is the record type
/// returned by a direct query. The same record may be used by several sources.
/// </typeparam>
/// <remarks>
/// <para>
/// The source's public name (registry, routes, events, telemetry) is its type name
/// (<c>Type.Name</c>), unmodified. Every local source can be queried directly at
/// <c>/{service}/queryable/{source}/query</c>, and through any query views that reference it.
/// Kaleido applies search, filter, sort and paging to the returned <see cref="IQueryable{T}"/>.
/// </para>
/// <para>
/// Implement this synchronous shape when the queryable is cheap to produce (in-memory data, or a
/// LINQ provider that defers execution). If producing it requires I/O, implement
/// <see cref="IQuerySourceAsync{TQueryContext}"/> instead — do not block inside
/// <see cref="CreateQuery"/>. A source implements exactly one of the two.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [QuerySource(
///     Version = "1.0.0",
///     DisplayName = "Members",
///     Description = "Member enrollments, searchable by member number and name.")]
/// internal sealed class MemberQuerySource(MemberDbContext db)
///     : IQuerySource&lt;MemberQueryContext&gt;
/// {
///     public IQueryable&lt;MemberQueryContext&gt; CreateQuery(QueryExecutionContext executionContext) =&gt;
///         db.MemberEnrollments.AsNoTracking().Select(e =&gt; new MemberQueryContext { /* ... */ });
/// }
/// </code>
/// </example>
public interface IQuerySource<TQueryContext> : ILocalQuerySource<TQueryContext>
    where TQueryContext : class, IQueryContext
{
    /// <summary>
    /// Creates the unfiltered queryable for this source. Kaleido applies the request's search,
    /// filter, sort and paging to it.
    /// </summary>
    /// <param name="executionContext">The source metadata and the current request.</param>
    /// <returns>The source's queryable.</returns>
    IQueryable<TQueryContext> CreateQuery(QueryExecutionContext executionContext);
}

/// <summary>
/// The asynchronous form of <see cref="IQuerySource{TQueryContext}"/> — implement it when producing
/// the queryable requires async I/O (remote fetch, async seed, async authorization resolution).
/// Implementing this interface is the source's <b>identity</b>; describe it with
/// <see cref="QuerySourceAttribute"/>.
/// </summary>
/// <typeparam name="TQueryContext">The query context record; see <see cref="IQuerySource{TQueryContext}"/>.</typeparam>
/// <remarks>
/// Everything documented on <see cref="IQuerySource{TQueryContext}"/> (naming, direct queries,
/// views) applies. A source implements exactly one of the two shapes.
/// </remarks>
public interface IQuerySourceAsync<TQueryContext> : ILocalQuerySource<TQueryContext>
    where TQueryContext : class, IQueryContext
{
    /// <summary>
    /// Creates the unfiltered queryable for this source. Kaleido applies the request's search,
    /// filter, sort and paging to it.
    /// </summary>
    /// <param name="executionContext">The source metadata and the current request.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The source's queryable.</returns>
    Task<IQueryable<TQueryContext>> CreateQueryAsync(
        QueryExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A local query view: a projection over a local source. Implementing this interface is the view's
/// <b>identity</b>; describe it with <see cref="QueryViewAttribute"/>.
/// </summary>
/// <typeparam name="TSource">
/// The local source this view projects — a type implementing
/// <see cref="IQuerySource{TQueryContext}"/> or <see cref="IQuerySourceAsync{TQueryContext}"/>.
/// The view is published under that source.
/// </typeparam>
/// <typeparam name="TQueryContext">The source's query context record (repeated so <see cref="CreateView"/> is fully typed).</typeparam>
/// <typeparam name="TView">The record type the view returns (a plain output shape).</typeparam>
/// <typeparam name="TViewParameters">The view's parameters record.</typeparam>
/// <remarks>
/// The view's public name is its type name (<c>Type.Name</c>), unique within its source; it is
/// queried at <c>/{service}/queryable/{source}/{view}/query</c>. Kaleido applies search, filter
/// and sort against <typeparamref name="TQueryContext"/>, then this projection, then paging.
/// A view implements exactly one of the synchronous or asynchronous shapes.
/// </remarks>
public interface IQueryViewSource<TSource, TQueryContext, TView, TViewParameters>
    where TSource : class, ILocalQuerySource<TQueryContext>
    where TQueryContext : class, IQueryContext
    where TView : class
    where TViewParameters : class, IQueryParameters
{
    /// <summary>Projects the source's queryable into the view's records.</summary>
    /// <param name="query">The source's queryable, with search, filter and sort applied.</param>
    /// <param name="executionContext">The source metadata and the current request.</param>
    /// <returns>The projected queryable.</returns>
    IQueryable<TView> CreateView(IQueryable<TQueryContext> query, QueryExecutionContext executionContext);
}

/// <summary>A local query view without parameters; see <see cref="IQueryViewSource{TSource,TQueryContext,TView,TViewParameters}"/>.</summary>
/// <typeparam name="TSource">The local source this view projects.</typeparam>
/// <typeparam name="TQueryContext">The source's query context record.</typeparam>
/// <typeparam name="TView">The record type the view returns.</typeparam>
public interface IQueryViewSource<TSource, TQueryContext, TView>
    : IQueryViewSource<TSource, TQueryContext, TView, EmptyQueryViewParameters>
    where TSource : class, ILocalQuerySource<TQueryContext>
    where TQueryContext : class, IQueryContext
    where TView : class;

/// <summary>
/// The asynchronous form of <see cref="IQueryViewSource{TSource,TQueryContext,TView,TViewParameters}"/> —
/// implement it when the projection requires async work. Implementing this interface is the view's
/// <b>identity</b>; describe it with <see cref="QueryViewAttribute"/>.
/// </summary>
/// <typeparam name="TSource">The local source this view projects.</typeparam>
/// <typeparam name="TQueryContext">The source's query context record.</typeparam>
/// <typeparam name="TView">The record type the view returns.</typeparam>
/// <typeparam name="TViewParameters">The view's parameters record.</typeparam>
public interface IQueryViewSourceAsync<TSource, TQueryContext, TView, TViewParameters>
    where TSource : class, ILocalQuerySource<TQueryContext>
    where TQueryContext : class, IQueryContext
    where TView : class
    where TViewParameters : class, IQueryParameters
{
    /// <summary>Projects the source's queryable into the view's records.</summary>
    /// <param name="query">The source's queryable, with search, filter and sort applied.</param>
    /// <param name="executionContext">The source metadata and the current request.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The projected queryable.</returns>
    Task<IQueryable<TView>> CreateViewAsync(
        IQueryable<TQueryContext> query,
        QueryExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}

/// <summary>An asynchronous local query view without parameters; see <see cref="IQueryViewSourceAsync{TSource,TQueryContext,TView,TViewParameters}"/>.</summary>
/// <typeparam name="TSource">The local source this view projects.</typeparam>
/// <typeparam name="TQueryContext">The source's query context record.</typeparam>
/// <typeparam name="TView">The record type the view returns.</typeparam>
public interface IQueryViewSourceAsync<TSource, TQueryContext, TView>
    : IQueryViewSourceAsync<TSource, TQueryContext, TView, EmptyQueryViewParameters>
    where TSource : class, ILocalQuerySource<TQueryContext>
    where TQueryContext : class, IQueryContext
    where TView : class;

/// <summary>
/// A delegated query source: a facade that hides internal complexity behind a simple query
/// capability. It receives the consumer's query (validated against
/// <typeparamref name="TQueryContext"/>), translates it into a downstream query — typically adding
/// internal criteria the consumer never sees — calls another service (a remote source or view),
/// and maps the response to its own <typeparamref name="TResult"/>. Implementing this interface is
/// the source's <b>identity</b>; describe it with <see cref="QuerySourceAttribute"/>.
/// </summary>
/// <typeparam name="TQueryContext">
/// The public query contract: which fields a consumer may search, filter and sort on. Kaleido
/// validates the consumer's query against it before calling <see cref="ExecuteAsync"/>.
/// </typeparam>
/// <typeparam name="TResult">The source's own result record — what the consumer receives (a plain output shape).</typeparam>
/// <typeparam name="TParameters">The source's parameters record (for example a process id).</typeparam>
/// <remarks>
/// <para>
/// Paging, filtering and sorting are performed downstream; Kaleido does not apply them to the
/// returned <see cref="QueryResult{T}"/>, and the paging information (total count, offset, page
/// size) is the downstream system's. Delegated sources have no query views.
/// </para>
/// <para>
/// The public name is the type name (<c>Type.Name</c>); the source is queried at
/// <c>/{service}/queryable/{source}/query</c>. To forward a query over HTTP, convert it with
/// <c>ToApiBody()</c> from <c>Kaleido.Http.Abstractions</c>.
/// </para>
/// </remarks>
public interface IDelegatedQuerySource<TQueryContext, TResult, TParameters>
    where TQueryContext : class, IQueryContext
    where TResult : class
    where TParameters : class, IQueryParameters
{
    /// <summary>Executes the query downstream and returns this source's mapped results.</summary>
    /// <param name="request">The consumer's validated query and parameters.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The mapped results with the downstream paging information.</returns>
    Task<QueryResult<TResult>> ExecuteAsync(
        IQueryRequest<TParameters> request,
        CancellationToken cancellationToken = default);
}

/// <summary>A delegated query source without parameters; see <see cref="IDelegatedQuerySource{TQueryContext,TResult,TParameters}"/>.</summary>
/// <typeparam name="TQueryContext">The public query contract.</typeparam>
/// <typeparam name="TResult">The source's own result record.</typeparam>
public interface IDelegatedQuerySource<TQueryContext, TResult>
    : IDelegatedQuerySource<TQueryContext, TResult, EmptyQueryViewParameters>
    where TQueryContext : class, IQueryContext
    where TResult : class;
