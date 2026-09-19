namespace Kaleido.Queryable.Query;

public interface IDelegateQueryViewSource<TDelegateContext, TView, TViewParameters>
        where TDelegateContext : class
        where TView : class
        where TViewParameters : class
{
    Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest<TViewParameters> request,
        CancellationToken cancellationToken = default);
}

public interface IDelegateQueryViewSource<TDelegateContext, TView> : IDelegateQueryViewSource<TDelegateContext, TView, EmptyQueryViewParameters>
    where TDelegateContext : class
    where TView : class
{
}
