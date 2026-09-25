using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable;

/// <summary>Declares a property as filterable.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class FilterableAttribute(
    params FilterOperator[] operators) : Attribute
{
    public IReadOnlyList<FilterOperator> Operators { get; } = operators;
}

/// <summary>Marks a record type as a framework-discoverable record.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class QueryContextAttribute : Attribute
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public string? DisplayName { get; init; }
    public string? Source { get; init; }
    public QueryContextKind Kind { get; init; } = QueryContextKind.Local;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class QueryViewAttribute : Attribute
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? DefaultSortField { get; init; }
    public string? Description { get; init; }
    public string? DisplayName { get; init; }
    public QueryViewVisibility Visibility { get; init; } = QueryViewVisibility.Public;
}

/// <summary>Declares paging support for a record.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class PageableAttribute : Attribute
{
    public int DefaultSize { get; init; }
    public int MaxSize { get; init; }
}

/// <summary>Declares a property as searchable.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SearchableAttribute : Attribute
{
    public int Priority { get; init; }
    public MatchMode MatchMode { get; init; }
}

/// <summary>Declares a property as sortable.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SortableAttribute : Attribute
{
}
