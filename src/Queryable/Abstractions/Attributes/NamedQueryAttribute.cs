using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NamedQueryAttribute : Attribute
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? DisplayName { get; init; }
}
