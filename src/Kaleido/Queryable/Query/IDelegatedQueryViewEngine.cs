using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

internal interface IDelegatedQueryViewEngine<TDelegateContext, TView>
    where TDelegateContext : class
    where TView : class
{
    Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest request,
        DelegatedQueryViewRegistration registration,
        CancellationToken cancellationToken = default);
}
