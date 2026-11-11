namespace Kaleido.Queryable.Query;

/// <summary>
/// What a query asks for: free-text search, a filter tree, sort order and a page. Every part is
/// optional.
/// </summary>
/// <remarks>
/// For a local source, Kaleido applies the search and the filter (both must match), then the sort,
/// to the source's query context records, and pages the result. A delegated source receives the
/// validated body and performs all of it downstream.
/// </remarks>
/// <param name="SearchText">
/// Free text matched against every searchable field of the query context, each with its own
/// <see cref="MatchMode"/>; a record matches when any searchable field matches. String matching
/// is case-insensitive. Null or whitespace means no search. Search text on a context with no
/// searchable fields fails with <see cref="QueryableErrorCodes.FieldNotSearchable"/>.
/// </param>
/// <param name="Filter">The filter tree, or <see langword="null"/> for no filter.</param>
/// <param name="Sort">
/// The sort order, or <see langword="null"/> or empty for the capability's default sort (its
/// default sort field, ascending, when it is pageable; otherwise the source's own order).
/// </param>
/// <param name="Page">
/// The page to return, or <see langword="null"/> for the capability's default page. Only a pageable
/// capability accepts a page.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record QueryBody
(
    string? SearchText = null,
    QueryFilterNode? Filter = null,
    IReadOnlyList<QuerySort>? Sort = null,
    QueryPage? Page = null
);

#region Sort/Page

/// <summary>
/// One key of a query's sort order.
/// </summary>
/// <remarks>
/// The field must exist (<see cref="QueryableErrorCodes.InvalidField"/>) and be sortable
/// (<see cref="QueryableErrorCodes.FieldNotSortable"/>), and may appear only once in a sort
/// (compared case-insensitively; <see cref="QueryableErrorCodes.DuplicateSortField"/>). The first
/// key orders the results and each further key breaks ties of the ones before it.
/// </remarks>
/// <param name="Field">The query context field to sort by, matched case-insensitively.</param>
/// <param name="Direction">The sort direction.</param>
/// <param name="Sequence">
/// The key's position in the sort order, lowest first. Keys without a sequence come after those
/// with one; keys with equal or no sequence keep their order in the list.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record QuerySort(
    string Field,
    SortDirection Direction,
    int? Sequence = null);

/// <summary>
/// The page of results a query asks for. Paging is accepted only for a source or view declared
/// <see cref="PageableAttribute"/>; otherwise the request fails with
/// <see cref="QueryableErrorCodes.PagingNotSupported"/>.
/// </summary>
/// <remarks>
/// A present page is an explicit page even when both values are omitted (for example
/// <c>{"page":{}}</c>), which selects the explicit-page <c>TotalCount</c> behaviour; an absent
/// page does not.
/// </remarks>
/// <param name="Size">
/// Results per page. Omitted (<see langword="null"/>) uses the capability's
/// <see cref="PageableAttribute.DefaultSize"/>. A supplied value must be greater than zero and no
/// greater than <see cref="PageableAttribute.MaxSize"/>
/// (<see cref="QueryableErrorCodes.InvalidPageSize"/>).
/// </param>
/// <param name="Offset">
/// Zero-based result offset. Omitted (<see langword="null"/>) means zero; a negative value fails
/// with <see cref="QueryableErrorCodes.InvalidPageOffset"/>.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record QueryPage(
    int? Size,
    int? Offset);

#endregion
