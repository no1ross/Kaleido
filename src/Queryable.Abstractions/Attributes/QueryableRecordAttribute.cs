namespace Kaleido.Queryable.Attributes;

/// <summary>Marks a record type as a framework-discoverable record.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class QueryableRecordAttribute : Attribute
{
    /// <summary>Creates record metadata for a record type.</summary>
    /// <param name="name">Logical record key used for lookup and query execution.</param>
    /// <param name="description">Version of the record metadata.</param>
    /// <param name="version">Version of the record metadata.</param>
    /// <param name="source">Name of the authoritative source for the record.</param>
    public QueryableRecordAttribute(string name, string? description = null, string? version = null, string? source = null)
    {
        Name = name;
        Description = description;
        Version = version;
        Source = source;
    }

    public string Name { get; }
    public string? Description { get; }
    public string? Version { get; }
    public string? Source { get; }
}
