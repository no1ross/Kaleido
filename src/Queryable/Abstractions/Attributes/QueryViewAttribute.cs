namespace Kaleido.Queryable.Attributes;

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

