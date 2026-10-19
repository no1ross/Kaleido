using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Kaleido;

/// <summary>
/// A validation constraint on a field, published in the registry metadata of Process step fields and
/// Queryable query parameters so clients and AI agents know which values are accepted. The framework
/// derives one per <see cref="ValidationAttribute"/> on the property at startup.
/// </summary>
/// <remarks>
/// The constraint only describes the attribute; the service still validates the values it receives.
/// </remarks>
[ExcludeFromCodeCoverage]
internal sealed record ConstraintContract
{
    /// <summary>
    /// The constraint name: the attribute's class name without the <c>Attribute</c> suffix, e.g.
    /// <c>Required</c>, <c>StringLength</c>, <c>MaxLength</c>, <c>MinLength</c>, <c>Range</c>,
    /// <c>RegularExpression</c>, <c>EmailAddress</c>, <c>Phone</c> or <c>Url</c>. Custom
    /// <see cref="ValidationAttribute"/> types are named the same way.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// The constraint's arguments: <c>MaximumLength</c> and <c>MinimumLength</c> for
    /// <c>StringLength</c>, <c>Length</c> for <c>MaxLength</c> and <c>MinLength</c>, <c>Minimum</c>
    /// and <c>Maximum</c> for <c>Range</c>, <c>Pattern</c> for <c>RegularExpression</c>. Empty for
    /// every other constraint, including custom attributes.
    /// </summary>
    public IReadOnlyCollection<ConstraintParameterContract> Parameters
    {
        get;
        init;
    }
        = [];
}

/// <summary>
/// One named argument of a <see cref="ConstraintContract"/>, e.g. <c>MaximumLength</c> of a
/// <c>StringLength</c> constraint.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ConstraintParameterContract
{
    /// <summary>The argument name, as listed in <see cref="ConstraintContract.Parameters"/>.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The argument value as read from the attribute, e.g. an <see cref="int"/> length, a
    /// <c>Range</c> bound or a <c>RegularExpression</c> pattern. After a JSON round trip of the
    /// registry it arrives as a <c>JsonElement</c>.
    /// </summary>
    public object? Value { get; init; }
}

internal interface IConstraintMapper
{
    IReadOnlyCollection<ConstraintContract> Map(PropertyInfo propertyInfo);
}

internal sealed class ConstraintMapper : IConstraintMapper
{
    public IReadOnlyCollection<ConstraintContract> Map(
        PropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        return propertyInfo
            .GetCustomAttributes<ValidationAttribute>()
            .Select(FromValidationAttribute)
            .ToArray();
    }

    private ConstraintContract FromValidationAttribute(
        ValidationAttribute attribute)
    {
        return attribute switch
        {
            RequiredAttribute =>
                Create<RequiredAttribute>(),

            StringLengthAttribute x =>
                Create<StringLengthAttribute>(
                    ("MaximumLength", x.MaximumLength),
                    ("MinimumLength", x.MinimumLength)),

            MaxLengthAttribute x =>
                Create<MaxLengthAttribute>(
                    ("Length", x.Length)),

            MinLengthAttribute x =>
                Create<MinLengthAttribute>(
                    ("Length", x.Length)),

            RangeAttribute x =>
                Create<RangeAttribute>(
                    ("Minimum", x.Minimum),
                    ("Maximum", x.Maximum)),

            RegularExpressionAttribute x =>
                Create<RegularExpressionAttribute>(
                    ("Pattern", x.Pattern)),

            EmailAddressAttribute =>
                Create<EmailAddressAttribute>(),

            PhoneAttribute =>
                Create<PhoneAttribute>(),

            UrlAttribute =>
                Create<UrlAttribute>(),

            _ =>
                Create(
                    GetConstraintName(
                        attribute.GetType())),
        };
    }

    private static ConstraintContract Create<TAttribute>(
        params (string Name, object? Value)[] parameters)
        where TAttribute : ValidationAttribute
    {
        return Create(
            GetConstraintName(
                typeof(TAttribute)),
            parameters);
    }

    private static ConstraintContract Create(
        string type,
        params (string Name, object? Value)[] parameters)
    {
        return new ConstraintContract
        {
            Type = type,

            Parameters = parameters
                .Select(x =>
                    new ConstraintParameterContract
                    {
                        Name = x.Name,
                        Value = x.Value,
                    })
                .ToArray(),
        };
    }

    private static string GetConstraintName(
        Type attributeType)
    {
        ArgumentNullException.ThrowIfNull(attributeType);

        const string suffix = "Attribute";

        return attributeType.Name.EndsWith(
            suffix,
            StringComparison.Ordinal)
            ? attributeType.Name[..^suffix.Length]
            : attributeType.Name;
    }
}

