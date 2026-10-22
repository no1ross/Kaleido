namespace Kaleido.Queryable;

/// <summary>
/// Declares an <see cref="IQueryContext"/> property as filterable with the given operators.
/// </summary>
/// <remarks>
/// Each operator must apply to the property's type, or startup fails with
/// <see cref="QueryableErrorCodes.InvalidRegistration"/> (analyzer KAL2020 reports it at compile
/// time): comparisons and <see cref="FilterOperator.Between"/> need a number or a type with
/// comparison operators (dates, times, <see cref="Guid"/>), string operators need a
/// <see langword="string"/>, <see cref="FilterOperator.IsTrue"/>/<see cref="FilterOperator.IsFalse"/> need a
/// <see langword="bool"/>, and <see cref="FilterOperator.IsNull"/>/<see cref="FilterOperator.IsNotNull"/> need a
/// nullable type.
/// </remarks>
/// <param name="operators">The filter operators consumers may use on this property.</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class FilterableAttribute(
    params FilterOperator[] operators) : Attribute
{
    /// <summary>The filter operators consumers may use on this property.</summary>
    public IReadOnlyList<FilterOperator> Operators { get; } = operators;
}

/// <summary>
/// Required descriptive metadata for a query source — a type implementing
/// <see cref="IQuerySource{TQueryContext}"/>, <see cref="IQuerySourceAsync{TQueryContext}"/> or
/// <see cref="IDelegatedQuerySource{TQueryContext,TResult,TParameters}"/>.
/// </summary>
/// <remarks>
/// This attribute describes a source; it does not identify one. The source's name is not declared
/// here — it is the type's name. <see cref="DisplayName"/> and <see cref="Description"/> are
/// published through the registry and are what UIs, generated documentation, and AI agents use to
/// understand the source, so write them for that audience. Startup fails if the attribute is
/// missing from a source, or is applied to a type that is not a source.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class QuerySourceAttribute : Attribute
{
    /// <summary>The source's version, published in the registry. Must be non-empty.</summary>
    public required string Version { get; init; }

    /// <summary>A short, human-readable name for the source (for example <c>"Members"</c>). Must be non-empty.</summary>
    public required string DisplayName { get; init; }

    /// <summary>What the source provides and when to use it, written for people and AI agents reading the registry. Must be non-empty.</summary>
    public required string Description { get; init; }

    /// <summary>Optional free-text description of where the data comes from (for example the owning system).</summary>
    public string? Source { get; init; }

    /// <summary>
    /// The query context property direct queries are sorted by when the request specifies no sort,
    /// so pages are stable. Required when a local source is <see cref="PageableAttribute">pageable</see>;
    /// the property must be <see cref="SortableAttribute">sortable</see>. Not used by delegated
    /// sources, whose order is their downstream's. Prefer a unique field (an id or code): rows with
    /// equal values can still change places between pages.
    /// </summary>
    public string? DefaultSortField { get; init; }
}

/// <summary>
/// Required descriptive metadata for a local query view — a type implementing
/// <see cref="IQueryViewSource{TSource,TQueryContext,TView,TViewParameters}"/> or
/// <see cref="IQueryViewSourceAsync{TSource,TQueryContext,TView,TViewParameters}"/>.
/// </summary>
/// <remarks>
/// This attribute describes a view; it does not identify one. The view's name is not declared
/// here — it is the type's name, unique within its source. Startup fails if the attribute is
/// missing from a view, or is applied to a type that is not a view.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class QueryViewAttribute : Attribute
{
    /// <summary>The view's version, published in the registry. Must be non-empty.</summary>
    public required string Version { get; init; }

    /// <summary>A short, human-readable name for the view (for example <c>"Member search"</c>). Must be non-empty.</summary>
    public required string DisplayName { get; init; }

    /// <summary>What the view returns and when to use it, written for people and AI agents reading the registry. Must be non-empty.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// The query context property used to sort when the request specifies no sort, so pages are
    /// stable. Required when the view is <see cref="PageableAttribute">pageable</see>; the property
    /// must be <see cref="SortableAttribute">sortable</see>. Prefer a unique field: rows with equal
    /// values can still change places between pages.
    /// </summary>
    public string? DefaultSortField { get; init; }
}

/// <summary>
/// Declares paging support. On a query source it applies to direct queries of that source; on a
/// query view it applies to that view. Paging is not inherited between them.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class PageableAttribute : Attribute
{
    /// <summary>The page size used when the request does not specify one.</summary>
    public int DefaultSize { get; init; }

    /// <summary>The largest page size a request may ask for.</summary>
    public int MaxSize { get; init; }
}

/// <summary>Declares an <see cref="IQueryContext"/> property as searchable by free text.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SearchableAttribute : Attribute
{
    /// <summary>The order in which searchable fields are considered (lower first).</summary>
    public int Priority { get; init; }

    /// <summary>How the search text is matched against this property.</summary>
    public MatchMode MatchMode { get; init; }
}

/// <summary>Declares an <see cref="IQueryContext"/> property as sortable.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SortableAttribute : Attribute;
