using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

internal interface IDelegatedQueryContextEngine<TQueryContext, TView>
    where TQueryContext : class
    where TView : class
{
    Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest request,
        QueryContextRegistration registration,
        CancellationToken cancellationToken = default);
}
