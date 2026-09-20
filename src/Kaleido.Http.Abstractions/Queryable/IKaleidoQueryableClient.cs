using Kaleido.Http.Abstractions.Queryable.Contracts;

namespace Kaleido.Http.Abstractions.Queryable;

public interface IKaleidoQueryableClient
{
    Task<IReadOnlyList<QueryableRecordResponse>> GetRegistryAsync(
        CancellationToken cancellationToken = default);

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
