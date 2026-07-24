//using Kaleido.Queryable;

//namespace Kaleido.Queryable.Metadata;

///// <summary>Runtime metadata used by validators, compilers, and engines.</summary>
//public sealed record RuntimeRecordMetadata(
//    string Key,
//    string Name,
//    string? Description,
//    string? Version,
//    string? Source,
//    IReadOnlyList<RuntimeFieldMetadata> Fields,
//    IReadOnlyList<RuntimeNamedQueryMetadata> NamedQueries,
//    RuntimePageableMetadata? Pageable);

///// <summary>Runtime field metadata, including CLR type information.</summary>
//public sealed record RuntimeFieldMetadata(
//    string Name,
//    Type FieldType,
//    bool IsFilterable,
//    IReadOnlyList<FilterOperator> FilterOperators,
//    bool IsSearchable,
//    int? SearchPriority,
//    IReadOnlyList<MatchMode> MatchModes,
//    bool IsSortable)
//{
//    public bool IsNullable => Nullable.GetUnderlyingType(FieldType) != null;
//};

///// <summary>Runtime metadata for an allowed named query.</summary>
//public sealed record RuntimeNamedQueryMetadata(string Name, string Description, IReadOnlyList<RuntimeQueryParameterMetadata>? Parameters = null);

//public sealed record RuntimeQueryParameterMetadata(
//    string Name,
//    Type Type,
//    bool Required,
//    string Description
//);
///// <summary>Runtime paging metadata.</summary>
//public sealed record RuntimePageableMetadata(int DefaultSize, int MaxSize);

///// <summary>Associates a record key with a record type and runtime metadata.</summary>
//public sealed record RecordRegistration(Type RecordType, RuntimeRecordMetadata RuntimeMetadata);
