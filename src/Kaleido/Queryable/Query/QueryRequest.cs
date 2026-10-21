namespace Kaleido.Queryable.Query;

/// <summary>
/// A query request whose parameters record is statically typed as <typeparamref name="TParameters"/>.
/// </summary>
/// <typeparam name="TParameters">
/// The parameters record of the query view or delegated source being queried
/// (<see cref="EmptyQueryViewParameters"/> when it declares none).
/// </typeparam>
/// <remarks>
/// A delegated source receives its request in this form; the request's parameters type must be the
/// source's declared parameters type.
/// </remarks>
public interface IQueryRequest<TParameters> : IQueryRequest
    where TParameters : class
{
    /// <summary>
    /// The typed parameters record, or <see langword="null"/> when the caller supplied none. A
    /// missing record is rejected during validation only when the record has a required member.
    /// </summary>
    new TParameters? ViewParameters { get; }
}

/// <summary>
/// A query against a Queryable source or view: the query body (search, filter, sort, paging) and
/// the parameters record. This is the untyped view of the request that the runtime validates,
/// compiles and passes to sources and views through <see cref="QueryExecutionContext"/>.
/// </summary>
/// <remarks>
/// Core request types are not serialized over HTTP directly; the transport converts its own wire
/// contract into a request before invoking the runtime.
/// </remarks>
public interface IQueryRequest
{
    /// <summary>
    /// The search, filter, sort and paging to apply, or <see langword="null"/> for none (every
    /// result, in the capability's default order and default page when it is pageable).
    /// </summary>
    QueryBody? Query { get; }

    /// <summary>
    /// The parameters record as an <see cref="object"/>, or <see langword="null"/> when none was
    /// supplied. Its DataAnnotations are validated before the query runs.
    /// </summary>
    object? ViewParameters { get; }

    /// <summary>
    /// The declared parameters type of the request (the <c>TParameters</c> of
    /// <see cref="IQueryRequest{TParameters}"/>), available even when
    /// <see cref="ViewParameters"/> is <see langword="null"/>. Parameter validation is skipped when
    /// it is <see cref="EmptyQueryViewParameters"/>.
    /// </summary>
    Type ViewParametersType { get; }
}

/// <summary>
/// A query request for a source or view that declares no parameters; its parameters record is
/// always an <see cref="EmptyQueryViewParameters"/> instance.
/// </summary>
/// <param name="Query">The search, filter, sort and paging to apply, or <see langword="null"/> for none.</param>
[ExcludeFromCodeCoverage]
public sealed record QueryRequest(
    QueryBody? Query = null)
    : QueryRequest<EmptyQueryViewParameters>(
        ViewParameters: new EmptyQueryViewParameters(),
        Query: Query);

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
public record QueryBody
(
    string? SearchText = null,
    QueryFilterNode? Filter = null,
    IReadOnlyList<QuerySort>? Sort = null,
    QueryPage? Page = null
);

#region Filters

/// <summary>
/// A test of one query context field: the field, the operator, and the operator's values.
/// </summary>
/// <remarks>
/// The field must exist (<see cref="QueryableErrorCodes.InvalidField"/>), be filterable
/// (<see cref="QueryableErrorCodes.FieldNotFilterable"/>) and declare the operator in its
/// <see cref="FilterableAttribute"/> (<see cref="QueryableErrorCodes.UnsupportedOperator"/>).
/// </remarks>
/// <param name="Field">
/// The query context field name, matched case-insensitively. Empty or whitespace fails with
/// <see cref="QueryableErrorCodes.MissingFilterField"/>.
/// </param>
/// <param name="Operator">The operator to apply; see <see cref="FilterOperator"/>.</param>
/// <param name="Values">
/// The operator's values. Comparison, equality and string operators use the first value;
/// <see cref="FilterOperator.In"/> and <see cref="FilterOperator.NotIn"/> use all of them;
/// <see cref="FilterOperator.Between"/> and <see cref="FilterOperator.NotBetween"/> use the first
/// two as lower and upper bound; the null and boolean operators use none. Non-null values must be
/// of a supported runtime type (<see cref="QueryableErrorCodes.UnsupportedRuntimeType"/>). When
/// Kaleido applies the filter to a local source, a missing required value fails with
/// <see cref="QueryableErrorCodes.InvalidFilterNode"/> and a value that cannot be converted to the
/// field's type with <see cref="QueryableErrorCodes.InvalidFilterValue"/>.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record QueryFilterCondition
(
    string Field,
    FilterOperator Operator,
    IReadOnlyList<object?> Values
);

/// <summary>
/// A group of filter nodes combined with one <see cref="LogicalOperator"/>.
/// </summary>
/// <param name="Operator">
/// <see cref="LogicalOperator.And"/> matches when every child matches;
/// <see cref="LogicalOperator.Or"/> when any child matches.
/// </param>
/// <param name="Filters">
/// The child nodes. An empty group fails with <see cref="QueryableErrorCodes.EmptyFilterGroup"/>.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record QueryFilterGroup
(
    LogicalOperator Operator,
    IReadOnlyList<QueryFilterNode> Filters
);

#endregion

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
public record QuerySort(
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
public record QueryPage(
    int? Size,
    int? Offset);

#endregion

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
