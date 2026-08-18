using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

internal interface IQueryContextEngine<TQueryContext, TView>
        where TQueryContext : class
        where TView : class
{
    Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest request,
        QueryContextRegistration registration,
        QueryViewRegistration viewRegistration,
        CancellationToken cancellationToken = default);

    Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest request,
        QueryContextRegistration registration,
        CancellationToken cancellationToken = default);
}
