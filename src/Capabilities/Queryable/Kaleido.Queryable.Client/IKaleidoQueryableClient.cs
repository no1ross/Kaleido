namespace Kaleido.Queryable.Client;

/// <summary>
/// Client for a remote service's Queryable capabilities. Sources and views are addressed by their
/// published names (strings): the caller lives in another service and cannot reference the remote
/// types. Each query uses its published transport binding from the host's hydrated unfiltered Registry.
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
    /// An adapter reports failures in its own transport's terms; the called service still enforces
    /// the source or view's authorization.
    /// </remarks>
    Task<QueryResult<TView>> QueryViewAsync<TParameters, TView>(
        string source,
        string view,
        QueryRequest<TParameters> request,
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
    /// An adapter reports failures in its own transport's terms; the called service still enforces
    /// the source or view's authorization.
    /// </remarks>
    Task<QueryResult<TResult>> QuerySourceAsync<TResult>(
        string source,
        QueryRequest request,
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
    /// An adapter reports failures in its own transport's terms; the called service still enforces
    /// the source or view's authorization.
    /// </remarks>
    Task<QueryResult<TResult>> QuerySourceAsync<TParameters, TResult>(
        string source,
        QueryRequest<TParameters> request,
        CancellationToken cancellationToken)
        where TParameters : class
        where TResult : class;
}
