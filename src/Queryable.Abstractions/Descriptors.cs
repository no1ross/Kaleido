//namespace Kaleido.Queryable;

///// <summary>Consumer-safe metadata for a registered record.</summary>
//public sealed record RecordDescriptor(
//    string Key,
//    string Name,
//    string? Version,
//    string? Description,
//    string? Source,
//    IReadOnlyList<FieldDescriptor> Fields,
//    IReadOnlyList<NamedQueryDescriptor> NamedQueries,
//    PageableDescriptor? Pageable);

///// <summary>Consumer-safe metadata for a record field.</summary>
//public sealed record FieldDescriptor(
//    string Name,
//    DataTypeDescriptor DataType,
//    bool IsFilterable,
//    IReadOnlyList<string> FilterOperators,
//    bool IsSearchable,
//    IReadOnlyList<string> MatchModes,
//    bool IsSortable);

///// <summary>OpenAPI/JSON-Schema-style data type shape.</summary>
//public sealed record DataTypeDescriptor(string Type, string? Format = null);

///// <summary>Consumer-safe allowed named-query metadata.</summary>
//public sealed record NamedQueryDescriptor(string Name, string Description, IReadOnlyList<QueryParameterDescriptor>? Parameters = null);

//public sealed record QueryParameterDescriptor
//(
//    string Name,
//    Type Type,
//    bool Required,
//    string Description
//);

///// <summary>Consumer-safe paging metadata.</summary>
//public sealed record PageableDescriptor(int DefaultSize, int MaxSize);
