using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Kaleido;

public sealed record ConstraintContract
{
    public required string Type { get; init; }

    public IReadOnlyCollection<ConstraintParameterContract> Parameters
    {
        get;
        init;
    }
        = [];
}

public sealed record ConstraintParameterContract
{
    public required string Name { get; init; }

    public object? Value { get; init; }
}


public static class ConstraintMapper
{
    public static IReadOnlyCollection<ConstraintContract> Map(
        PropertyInfo property)
    { 
        ArgumentNullException.ThrowIfNull(property);

        return property
            .GetCustomAttributes<ValidationAttribute>()
            .Select(FromValidationAttribute)
            .ToArray();
    }

    private static ConstraintContract FromValidationAttribute(
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
                        attribute.GetType()))
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
                        Value = x.Value
                    })
                .ToArray()
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

