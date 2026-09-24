using Kaleido.Http.Queryable.Contracts;

namespace Kaleido.Http.Queryable;

public interface IKaleidoQueryableClient
{
    Task<IReadOnlyList<QueryableRecordResponse>> GetRegistryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the local registry cache so the next operation re-fetches
    /// the registry from the remote endpoint.
    /// </summary>
    void InvalidateRegistry();

    Task<QueryableRecordResponse> GetContextMetadataAsync(
        string context,
        CancellationToken cancellationToken = default);

    Task<QueryResult<TView>> QueryViewAsync<TParameters, TView>(
        string context,
        string view,
        QueryApiRequest<TParameters> request,
        CancellationToken cancellationToken = default)
        where TParameters : class
        where TView : class;

    Task<QueryResult<TView>> QueryContextAsync<TView>(
        string context,
        QueryApiRequest request,
        CancellationToken cancellationToken = default)
        where TView : class;
}
