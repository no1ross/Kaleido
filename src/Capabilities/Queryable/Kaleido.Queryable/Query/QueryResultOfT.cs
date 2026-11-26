namespace Kaleido.Queryable.Query;

/// <summary>
/// The result of a query: the returned page of records and the paging it was produced with.
/// </summary>
/// <typeparam name="TView">
/// The returned record type: the query context for a direct query, the view record for a view
/// query, or the delegated source's result record.
/// </typeparam>
/// <param name="TotalCount">
/// The number of matching results, whatever page was returned. For a pageable capability the
/// results are counted only when the page cannot tell (a full page, or an empty page past the
/// start); a partial page ends the results, so the total is <c>Offset</c> plus the returned count.
/// An unpageable capability returns every result, so the total is the returned count. A delegated
/// source's total is its downstream's. When the results are counted, the count is a second read of
/// the source: rows another writer changes between the two reads can make the total disagree with the
/// page (Kaleido reads no snapshot).
/// </param>
/// <param name="Offset">
/// The zero-based offset of the page (zero when the request named none). A delegated source's
/// offset is its downstream's.
/// </param>
/// <param name="PageSize">
/// The page size the query used: the requested size, otherwise the capability's
/// <see cref="PageableAttribute.DefaultSize"/>. An unpageable capability reports a nominal size
/// (currently 50) that does not limit <c>Results</c>. A delegated source's page size is its
/// downstream's.
/// </param>
/// <param name="Results">The returned records, in sort order.</param>
[ExcludeFromCodeCoverage]
public sealed record QueryResult<TView>(int TotalCount, int Offset, int PageSize, IReadOnlyCollection<TView> Results)
    where TView : class;
