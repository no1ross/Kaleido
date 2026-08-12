namespace Kaleido.Queryable.Query;

public interface IQueryViewSource<TQueryView, TView, TViewParameters>
        where TQueryView : class
        where TView : class
        where TViewParameters : class
{
    IQueryable<TView> CreateView(IQueryable<TQueryView> query, QueryExecutionContext executionContext);
}


public interface IQueryViewSource<TQueryView, TView> : IQueryViewSource<TQueryView, TView, EmptyQueryViewParameters> 
    where TQueryView : class
    where TView : class
{
}

public sealed record EmptyQueryViewParameters;

