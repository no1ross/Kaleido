namespace Kaleido.Attributes;

/// <summary>Marks a record type as a framework-discoverable record.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class KaleidoRecordAttribute : Attribute
{
    /// <summary>Creates record metadata for a record type.</summary>
    /// <param name="name">Logical record key used for lookup and query execution.</param>
    /// <param name="version">Version of the record metadata.</param>
    /// <param name="source">Name of the authoritative source for the record.</param>
    public KaleidoRecordAttribute(string name, string version, string source)
    {
        Name = name;
        Version = version;
        Source = source;
    }

    public string Name { get; }
    public string Version { get; }
    public string Source { get; }
}
