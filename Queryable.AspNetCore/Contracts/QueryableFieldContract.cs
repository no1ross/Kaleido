using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.AspNetCore;

/// <summary>
/// API-safe field metadata for a queryable registration.
/// </summary>
public sealed record QueryableFieldContract
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public bool IsNullable { get; init; }

    public static QueryableFieldContract FromField(RuntimeFieldMetadata field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return new QueryableFieldContract
        {
            Name = field.Name,
            Type = field.FieldType.Name,
            IsNullable = field.IsNullable
        };
    }
}
