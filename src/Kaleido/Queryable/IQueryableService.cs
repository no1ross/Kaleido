using Kaleido.Queryable.Query;

namespace Kaleido.Queryable;

public interface IQueryableService
{
    Task<QueryResult<TView>> QueryAsync<TQueryView, TView>(IQueryRequest request, CancellationToken cancellationToken = default) 
        where TQueryView : class
        where TView : class;
}