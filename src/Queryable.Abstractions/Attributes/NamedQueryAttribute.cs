using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.Attributes;

/// <summary>Declares a named query that is allowed for a record.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NamedQueryAttribute : Attribute
{
    /// <summary>Creates allowed-query metadata.</summary>
    /// <param name="name">Named-query key accepted by <see cref="QueryRequest.QueryName"/>.</param>
    /// <param name="description">Short description of what the named query returns.</param>
    public NamedQueryAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public string Name { get; }
    public string Description { get; }
}
