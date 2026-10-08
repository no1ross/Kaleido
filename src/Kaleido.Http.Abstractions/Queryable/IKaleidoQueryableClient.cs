namespace Kaleido.Http.Queryable;

/// <summary>
/// Client for a remote service's Queryable capabilities. Sources and views are addressed by their
/// published names (strings): the caller lives in another service and cannot reference the remote
/// types. Names are resolved against the remote registry (case-insensitive).
/// </summary>
public interface IKaleidoQueryableClient
{
    /// <summary>Gets the remote service's query sources (fetched once and cached).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The remote query sources with their views.</returns>
    Task<IReadOnlyList<QueryableSourceResponse>> GetRegistryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the local registry cache so the next operation re-fetches
    /// the registry from the remote endpoint.
    /// </summary>
    void InvalidateRegistry();

    /// <summary>Queries a view of a remote query source.</summary>
    /// <typeparam name="TParameters">The view's parameters record (a local mirror is fine).</typeparam>
    /// <typeparam name="TView">The view's record type (a local mirror is fine).</typeparam>
    /// <param name="source">The source's published name.</param>
    /// <param name="view">The view's published name.</param>
    /// <param name="request">The query and parameters.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The query result.</returns>
    /// <remarks>
    /// The HTTP client implementation throws <c>KaleidoHttpClientException</c> when the source or
    /// view is not in the remote registry, or the remote call fails.
    /// </remarks>
    Task<QueryResult<TView>> QueryViewAsync<TParameters, TView>(
        string source,
        string view,
        QueryApiRequest<TParameters> request,
        CancellationToken cancellationToken = default)
        where TParameters : class
        where TView : class;

    /// <summary>Queries a remote query source directly, without parameters.</summary>
    /// <typeparam name="TResult">The source's result record type (a local mirror is fine).</typeparam>
    /// <param name="source">The source's published name.</param>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The query result.</returns>
    /// <remarks>
    /// The HTTP client implementation throws <c>KaleidoHttpClientException</c> when the source is
    /// not in the remote registry, or the remote call fails.
    /// </remarks>
    Task<QueryResult<TResult>> QuerySourceAsync<TResult>(
        string source,
        QueryApiRequest request,
        CancellationToken cancellationToken = default)
        where TResult : class;

    /// <summary>Queries a remote query source directly, with parameters.</summary>
    /// <typeparam name="TParameters">The source's parameters record (a local mirror is fine).</typeparam>
    /// <typeparam name="TResult">The source's result record type (a local mirror is fine).</typeparam>
    /// <param name="source">The source's published name.</param>
    /// <param name="request">The query and parameters.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The query result.</returns>
    /// <remarks>
    /// The HTTP client implementation throws <c>KaleidoHttpClientException</c> when the source is
    /// not in the remote registry, or the remote call fails.
    /// </remarks>
    Task<QueryResult<TResult>> QuerySourceAsync<TParameters, TResult>(
        string source,
        QueryApiRequest<TParameters> request,
        CancellationToken cancellationToken = default)
        where TParameters : class
        where TResult : class;
}
