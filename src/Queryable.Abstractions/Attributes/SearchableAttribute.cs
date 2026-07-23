using Kaleido.Queryable;

namespace Kaleido.Queryable.Attributes;

/// <summary>Declares a property as searchable.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SearchableAttribute : Attribute
{
    /// <summary>Creates search metadata for a property.</summary>
    /// <param name="priority">Priority used when a search applies to multiple fields.</param>
    /// <param name="defaultMode">The default match mode for this property.</param>
    /// <param name="matchModes">Search match modes supported by this property.</param>
    public SearchableAttribute(int priority, params MatchMode[] matchModes)
    {
        Priority = priority;
        MatchModes = matchModes.Length == 0 ? new[] { MatchMode.StartsWith } : matchModes;
    }

    public int Priority { get; }
    public IReadOnlyList<MatchMode> MatchModes { get; }
}
