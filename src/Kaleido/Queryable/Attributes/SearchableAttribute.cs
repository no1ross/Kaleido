using Kaleido.Queryable;

namespace Kaleido.Queryable.Attributes;

/// <summary>Declares a property as searchable.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SearchableAttribute : Attribute
{
    public int Priority { get; init; }
    public MatchMode MatchMode { get; init; }
}
