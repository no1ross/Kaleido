namespace Kaleido.Attributes;

/// <summary>Declares a named query that is allowed for a record.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class AllowedQueryAttribute : Attribute
{
    /// <summary>Creates allowed-query metadata.</summary>
    /// <param name="name">Named-query key accepted by <see cref="KaleidoQueryRequest.QueryName"/>.</param>
    /// <param name="description">Short description of what the named query returns.</param>
    /// <param name="parameters">Optional required parameter names.</param>
    public AllowedQueryAttribute(string name, string description, params string[] parameters)
    {
        Name = name;
        Description = description;
        Parameters = parameters;
    }

    public string Name { get; }
    public string Description { get; }
    public IReadOnlyList<string> Parameters { get; }
}
