namespace Kaleido;

/// <summary>Consumer-safe metadata for a registered record.</summary>
public sealed record RecordDescriptor(
    string Name,
    string Version,
    string Source,
    IReadOnlyList<FieldDescriptor> Fields,
    IReadOnlyList<AllowedQueryDescriptor> AllowedQueries,
    PageableDescriptor? Pageable);

/// <summary>Consumer-safe metadata for a record field.</summary>
public sealed record FieldDescriptor(
    string Name,
    DataTypeDescriptor DataType,
    bool IsFilterable,
    IReadOnlyList<string> FilterOperators,
    bool IsSearchable,
    IReadOnlyList<string> MatchModes,
    bool IsSortable);

/// <summary>OpenAPI/JSON-Schema-style data type shape.</summary>
public sealed record DataTypeDescriptor(string Type, string? Format = null);

/// <summary>Consumer-safe allowed named-query metadata.</summary>
public sealed record AllowedQueryDescriptor(string Name, string Description, IReadOnlyList<string> Parameters);

/// <summary>Consumer-safe paging metadata.</summary>
public sealed record PageableDescriptor(int DefaultSize, int MaxSize);
