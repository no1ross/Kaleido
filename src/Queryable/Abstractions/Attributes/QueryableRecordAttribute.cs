namespace Kaleido.Queryable.Attributes;

/// <summary>Marks a record type as a framework-discoverable record.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class QueryableRecordAttribute : Attribute
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public string? DisplayName { get; init; }
    public string? Source { get; init; }
}


[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RecordViewAttribute : Attribute
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public string? DisplayName { get; init; }
    public required bool ApplyPaging { get; init; }
    public required bool ApplySorting { get; init; }
}

