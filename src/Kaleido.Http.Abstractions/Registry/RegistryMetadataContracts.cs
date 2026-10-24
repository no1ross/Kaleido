namespace Kaleido.Http.Registry;

/// <summary>
/// The published description of a field's type, in the registry metadata of Process step fields
/// and Queryable fields, parameters and results, so clients and AI agents know what value to send
/// or expect.
/// </summary>
/// <remarks>
/// Scalars are <c>"string"</c>, <c>"boolean"</c>, <c>"integer"</c> or <c>"number"</c>, with a
/// <paramref name="Format"/> where the base type alone is ambiguous; enums are <c>"string"</c>
/// with format <c>"enum"</c> and their members in <paramref name="EnumValues"/>; arrays are
/// <c>"array"</c> with an <paramref name="ItemType"/>; a dictionary (string keys) is <c>"object"</c> with
/// its value type as <paramref name="ItemType"/>; every other type is <c>"object"</c>.
/// </remarks>
/// <param name="Type">
/// The base type: <c>"string"</c>, <c>"boolean"</c>, <c>"integer"</c>, <c>"number"</c>, <c>"array"</c> or
/// <c>"object"</c>.
/// </param>
/// <param name="Format">
/// Refines <paramref name="Type"/>, or <see langword="null"/> when the base type is enough:
/// <c>"int64"</c>, <c>"float"</c>, <c>"double"</c>, <c>"decimal"</c>, <c>"uuid"</c>, <c>"date"</c>,
/// <c>"time"</c>, <c>"date-time"</c>, <c>"date-time-offset"</c>, <c>"duration"</c> or <c>"enum"</c>.
/// </param>
/// <param name="Nullable"><see langword="true"/> when the value may be <see langword="null"/>.</param>
/// <param name="EnumValues">The members of an enum type (format <c>"enum"</c>); <see langword="null"/> for every other type.</param>
/// <param name="ItemType">The type of the elements of an <c>"array"</c>, or of the values of an <c>"object"</c> that is a dictionary (its keys are the JSON property names); <see langword="null"/> for every other type.</param>
[ExcludeFromCodeCoverage]
public sealed record DataTypeMetadata(
    string Type,
    string? Format = null,
    bool Nullable = false,
    IReadOnlyCollection<EnumValueMetadata>? EnumValues = null,
    DataTypeMetadata? ItemType = null);

/// <summary>One member of an enum type, listed in <see cref="DataTypeMetadata.EnumValues"/>.</summary>
/// <param name="Value">The member's underlying numeric value.</param>
/// <param name="Name">The member's name, as declared in code; the value to send.</param>
/// <param name="Description">The member's description, or <see langword="null"/> when it has none.</param>
[ExcludeFromCodeCoverage]
public sealed record EnumValueMetadata(
    int Value,
    string Name,
    string? Description);

/// <summary>
/// A validation constraint on a field, in the registry metadata of Process step fields and
/// Queryable query parameters, so clients and AI agents know which values are accepted.
/// </summary>
/// <remarks>
/// The constraint only describes the field's validation attribute; the service still validates the
/// values it receives.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record ConstraintMetadata
{
    /// <summary>
    /// The constraint name: the validation attribute's class name without the <c>Attribute</c>
    /// suffix, e.g. <c>Required</c>, <c>StringLength</c>, <c>MaxLength</c>, <c>MinLength</c>,
    /// <c>Range</c>, <c>RegularExpression</c>, <c>EmailAddress</c>, <c>Phone</c> or <c>Url</c>.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// The constraint's arguments: <c>MaximumLength</c> and <c>MinimumLength</c> for
    /// <c>StringLength</c>, <c>Length</c> for <c>MaxLength</c> and <c>MinLength</c>, <c>Minimum</c>
    /// and <c>Maximum</c> for <c>Range</c>, <c>Pattern</c> for <c>RegularExpression</c>. Empty for
    /// every other constraint.
    /// </summary>
    public IReadOnlyCollection<ConstraintParameterMetadata> Parameters { get; init; } = [];
}

/// <summary>
/// One named argument of a <see cref="ConstraintMetadata"/>, e.g. <c>MaximumLength</c> of a
/// <c>StringLength</c> constraint.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ConstraintParameterMetadata
{
    /// <summary>The argument name, as listed in <see cref="ConstraintMetadata.Parameters"/>.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The argument value: a length, a <c>Range</c> bound or a <c>RegularExpression</c> pattern. A
    /// deserialized registry carries it as a <c>JsonElement</c>.
    /// </summary>
    public object? Value { get; init; }
}
