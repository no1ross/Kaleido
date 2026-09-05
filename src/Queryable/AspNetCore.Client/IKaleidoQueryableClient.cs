using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.AspNetCore.Client;

public interface IKaleidoQueryableClient
{
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
