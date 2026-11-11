namespace Kaleido.Queryable;

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
/// <c>/{service}/{version}/queryable/{source}/query</c>, and through any query views that reference it.
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
        CancellationToken cancellationToken);
}

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
/// <c>/{service}/{version}/queryable/{source}/query</c>. To forward a query over HTTP, convert it with
/// <c>ToApiBody()</c> from <c>Kaleido.Platform</c>.
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
        CancellationToken cancellationToken);
}

/// <summary>A delegated query source without parameters; see <see cref="IDelegatedQuerySource{TQueryContext,TResult,TParameters}"/>.</summary>
/// <typeparam name="TQueryContext">The public query contract.</typeparam>
/// <typeparam name="TResult">The source's own result record.</typeparam>
public interface IDelegatedQuerySource<TQueryContext, TResult>
    : IDelegatedQuerySource<TQueryContext, TResult, EmptyQueryViewParameters>
    where TQueryContext : class, IQueryContext
    where TResult : class;
