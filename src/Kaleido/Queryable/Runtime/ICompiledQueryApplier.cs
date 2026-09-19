using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.Runtime;

internal interface ICompiledQueryApplier<TQueryContext> where TQueryContext : class
{
    IQueryable<TQueryContext> ApplyFilter(IQueryable<TQueryContext> query, CompiledFilterExpression? filter);
    IQueryable<TQueryContext> ApplySearch(IQueryable<TQueryContext> query, CompiledSearch? search);
    IQueryable<TQueryContext> ApplySort(IQueryable<TQueryContext> query, IReadOnlyList<CompiledSort> sort);
}