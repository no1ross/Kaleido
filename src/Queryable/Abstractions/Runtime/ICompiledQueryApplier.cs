using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.Runtime
{
    internal interface ICompiledQueryApplier<TRecord> where TRecord : class
    {
        IQueryable<TRecord> ApplyFilter(IQueryable<TRecord> query, CompiledFilterExpression? filter);
        IQueryable<TRecord> ApplySearch(IQueryable<TRecord> query, CompiledSearchExpression? search);
        IQueryable<TRecord> ApplySort(IQueryable<TRecord> query, IReadOnlyList<CompiledSort> sort);
        IQueryable<TRecord> ApplyPage(IQueryable<TRecord> query, CompiledPage page);
    }
}