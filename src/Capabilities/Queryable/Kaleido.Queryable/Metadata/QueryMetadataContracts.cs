namespace Kaleido.Queryable.Metadata;

/// <summary>A query context field and its query rules.</summary>
[ExcludeFromCodeCoverage]
internal sealed record FieldMetadata
{
    /// <summary>The field's name.</summary>
    public required string Name { get; init; }

    /// <summary>The field's description (<c>[Description]</c>), if any.</summary>
    public string? Description { get; init; }

    /// <summary>The field's CLR type.</summary>
    public required Type FieldType { get; init; }

    /// <summary>The field's published data type.</summary>
    public required DataTypeDescriptor DataType { get; init; }

    /// <summary>True when the field can be filtered (<c>[Filterable]</c>).</summary>
    public bool IsFilterable { get; init; }

    /// <summary>The filter operators the field allows.</summary>
    public IReadOnlyList<FilterOperator> FilterOperators { get; init; } = [];

    /// <summary>True when the field takes part in free-text search (<c>[Searchable]</c>).</summary>
    public bool IsSearchable { get; init; }

    /// <summary>The field's search priority, when searchable.</summary>
    public int? SearchPriority { get; init; }

    /// <summary>How search text matches the field, when searchable.</summary>
    public MatchMode? MatchMode { get; init; }

    /// <summary>True when results can be sorted by the field (<c>[Sortable]</c>).</summary>
    public bool IsSortable { get; init; }
    /// <summary>The field's data classifications (<c>DataClassificationAttribute</c>, e.g. <c>"PriorAuth:PHI"</c>); empty when not classified.</summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];
}

/// <summary>Paging limits, and the order pages follow when the request specifies no sort.</summary>
/// <param name="DefaultSize">The page size used when the request does not specify one.</param>
/// <param name="MaxSize">The largest page size a request may ask for.</param>
/// <param name="DefaultSortField">
/// The query context field a request without a sort is ordered by (ascending), so pages are stable.
/// Declared by the view (<see cref="QueryViewAttribute.DefaultSortField"/>) or the local source
/// (<see cref="QuerySourceAttribute.DefaultSortField"/>); <see langword="null"/> for a delegated
/// source, whose order is its downstream's.
/// </param>
[ExcludeFromCodeCoverage]
internal sealed record PageableMetadata
(
    int DefaultSize,
    int MaxSize,
    string? DefaultSortField = null
);

/// <summary>A query parameter and its constraints.</summary>
[ExcludeFromCodeCoverage]
internal sealed record QueryParameterMetadata
{
    /// <summary>The parameter's name.</summary>
    public required string Name { get; init; }

    /// <summary>The parameter's CLR type.</summary>
    public required Type Type { get; init; }

    /// <summary>The parameter's published data type.</summary>
    public required DataTypeDescriptor DataType { get; init; }

    /// <summary>The parameter's validation constraints.</summary>
    public IReadOnlyCollection<ConstraintContract> Constraints { get; init; } = [];

    /// <summary>The parameter's description (<c>[Description]</c>), if any.</summary>
    public string? Description { get; init; }
    /// <summary>The field's data classifications (<c>DataClassificationAttribute</c>, e.g. <c>"PriorAuth:PHI"</c>); empty when not classified.</summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];
}

/// <summary>A field of a returned record.</summary>
[ExcludeFromCodeCoverage]
internal sealed record QueryOutputFieldMetadata
{
    /// <summary>The field's name.</summary>
    public required string Name { get; init; }

    /// <summary>The field's description (<c>[Description]</c>), if any.</summary>
    public string? Description { get; init; }

    /// <summary>The field's CLR type.</summary>
    public required Type Type { get; init; }

    /// <summary>The field's published data type.</summary>
    public required DataTypeDescriptor DataType { get; init; }
    /// <summary>The field's data classifications (<c>DataClassificationAttribute</c>, e.g. <c>"PriorAuth:PHI"</c>); empty when not classified.</summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];
}
