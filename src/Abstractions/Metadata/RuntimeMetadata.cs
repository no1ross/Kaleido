namespace Kaleido.Metadata;

/// <summary>Runtime metadata used by validators, compilers, and engines.</summary>
public sealed record RuntimeRecordMetadata(
    string Name,
    string Version,
    string Source,
    IReadOnlyList<RuntimeFieldMetadata> Fields,
    IReadOnlyList<RuntimeAllowedQueryMetadata> AllowedQueries,
    RuntimePageableMetadata? Pageable);

/// <summary>Runtime field metadata, including CLR type information.</summary>
public sealed record RuntimeFieldMetadata(
    string Name,
    Type FieldType,
    bool IsFilterable,
    IReadOnlyList<FilterOperator> FilterOperators,
    bool IsSearchable,
    int? SearchPriority,
    IReadOnlyList<MatchMode> MatchModes,
    bool IsSortable);

/// <summary>Runtime metadata for an allowed named query.</summary>
public sealed record RuntimeAllowedQueryMetadata(string Name, string Description, IReadOnlyList<string> Parameters);

/// <summary>Runtime paging metadata.</summary>
public sealed record RuntimePageableMetadata(int DefaultSize, int MaxSize);

/// <summary>Associates a record key with a record type and runtime metadata.</summary>
public sealed record RecordRegistration(string Name, Type RecordType, RuntimeRecordMetadata RuntimeMetadata);
