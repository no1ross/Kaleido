using Kaleido.Queryable;

namespace Kaleido.Queryable.Attributes;

/// <summary>Declares a property as searchable.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SearchableAttribute : Attribute
{
    public SearchableAttribute(int priority = 0, params MatchMode[] matchModes)
    {
        Priority = priority;
        MatchModes = matchModes.Length == 0 ? new[] { MatchMode.StartsWith } : matchModes;
    }

    public int Priority { get; }
    public IReadOnlyList<MatchMode> MatchModes { get; }
}
