namespace Kaleido.Queryable;

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
/// queried at <c>/{service}/{version}/queryable/{source}/{view}/query</c>. Kaleido applies search, filter
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
        CancellationToken cancellationToken);
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
