using Kaleido.Platform.Queryable;

namespace Kaleido.Client.Queryable;

/// <summary>
/// Client for a remote service's Queryable capabilities. Sources and views are addressed by their
/// published names (strings): the caller lives in another service and cannot reference the remote
/// types. A query goes to <c>/{routePrefix}/queryable/{source}[/{view}]/query</c> without consulting the
/// registry, which is filtered per caller and serves discovery only.
/// </summary>
public interface IKaleidoQueryableClient
{
    /// <summary>Queries a view of a remote query source.</summary>
    /// <typeparam name="TParameters">The view's parameters record (a local mirror is fine).</typeparam>
    /// <typeparam name="TView">The view's record type (a local mirror is fine).</typeparam>
    /// <param name="source">The source's published name.</param>
    /// <param name="view">The view's published name.</param>
    /// <param name="request">The query and parameters.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The query result.</returns>
    /// <remarks>
    /// The HTTP client implementation throws <c>KaleidoHttpClientException</c> when the remote call fails;
    /// a source or view the service does not have, or the caller may not use, arrives as the
    /// service's own 404 or 403.
    /// </remarks>
    Task<QueryResult<TView>> QueryViewAsync<TParameters, TView>(
        string source,
        string view,
        QueryApiRequest<TParameters> request,
        CancellationToken cancellationToken)
        where TParameters : class
        where TView : class;

    /// <summary>Queries a remote query source directly, without parameters.</summary>
    /// <typeparam name="TResult">The source's result record type (a local mirror is fine).</typeparam>
    /// <param name="source">The source's published name.</param>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The query result.</returns>
    /// <remarks>
    /// The HTTP client implementation throws <c>KaleidoHttpClientException</c> when the remote call fails;
    /// a source or view the service does not have, or the caller may not use, arrives as the
    /// service's own 404 or 403.
    /// </remarks>
    Task<QueryResult<TResult>> QuerySourceAsync<TResult>(
        string source,
        QueryApiRequest request,
        CancellationToken cancellationToken)
        where TResult : class;

    /// <summary>Queries a remote query source directly, with parameters.</summary>
    /// <typeparam name="TParameters">The source's parameters record (a local mirror is fine).</typeparam>
    /// <typeparam name="TResult">The source's result record type (a local mirror is fine).</typeparam>
    /// <param name="source">The source's published name.</param>
    /// <param name="request">The query and parameters.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The query result.</returns>
    /// <remarks>
    /// The HTTP client implementation throws <c>KaleidoHttpClientException</c> when the remote call fails;
    /// a source or view the service does not have, or the caller may not use, arrives as the
    /// service's own 404 or 403.
    /// </remarks>
    Task<QueryResult<TResult>> QuerySourceAsync<TParameters, TResult>(
        string source,
        QueryApiRequest<TParameters> request,
        CancellationToken cancellationToken)
        where TParameters : class
        where TResult : class;
}
