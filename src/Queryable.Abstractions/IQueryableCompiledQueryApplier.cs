namespace Kaleido.Queryable
{
    /// <summary>
    /// Applies compiled query components to an IQueryable source.
    ///
    /// This abstraction isolates LINQ/expression-tree generation
    /// from query orchestration.
    ///
    /// Responsibilities:
    ///   - Filter application
    ///   - Search application
    ///   - Sort application
    ///   - Paging application
    ///
    /// Non-Responsibilities:
    ///   - Validation
    ///   - Metadata generation
    ///   - Query orchestration
    ///   - Result materialization
    ///
    /// Used By:
    ///     QueryableRecordQueryEngine<TRecord>
    ///
    /// Primary Benefit:
    ///     Allows alternative execution strategies to be introduced
    ///     without modifying query orchestration logic.
    /// </summary>
    public interface IQueryableCompiledQueryApplier<TRecord> where TRecord : class
    {
        IQueryable<TRecord> ApplyFilter(IQueryable<TRecord> query, CompiledFilterExpression? filter);
        IQueryable<TRecord> ApplySearch(IQueryable<TRecord> query, CompiledSearchExpression? search);
        IQueryable<TRecord> ApplySort(IQueryable<TRecord> query, IReadOnlyList<CompiledSort> sort);
        IQueryable<TRecord> ApplyPage(IQueryable<TRecord> query, CompiledPage page);
    }
}