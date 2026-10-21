using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kaleido.Analyzers;

internal static class AttributeHelper
{
    private const string DescriptionAttributeFullName = "System.ComponentModel.DescriptionAttribute";
    private const string DisplayAttributeFullName = "System.ComponentModel.DataAnnotations.DisplayAttribute";

    // [Description("...")] or [Display(Description = "...")] with non-empty text: what the registry
    // publishes as the field's description.
    public static bool HasDescription(IPropertySymbol property)
    {
        foreach (var attribute in property.GetAttributes())
        {
            switch (attribute.AttributeClass?.ToDisplayString())
            {
                case DescriptionAttributeFullName
                    when attribute.ConstructorArguments.Length == 1 &&
                         attribute.ConstructorArguments[0].Value is string text &&
                         !string.IsNullOrWhiteSpace(text):
                    return true;

                case DisplayAttributeFullName:
                    foreach (var argument in attribute.NamedArguments)
                    {
                        if (argument.Key == "Description" &&
                            argument.Value.Value is string description &&
                            !string.IsNullOrWhiteSpace(description))
                        {
                            return true;
                        }
                    }

                    break;
            }
        }

        return false;
    }

    // The named argument's compile-time value - a literal, a concatenation of literals, a const or
    // nameof(...) - must be a non-empty string. Attribute arguments are always constants, so the
    // semantic model has the value the runtime will see.
    public static bool HasNonEmptyNamedArgument(
        SeparatedSyntaxList<AttributeArgumentSyntax> args,
        string parameterName,
        SemanticModel semanticModel)
    {
        foreach (var arg in args)
        {
            if (arg.NameEquals?.Name.Identifier.Text != parameterName)
            {
                continue;
            }

            return semanticModel.GetConstantValue(arg.Expression) is { HasValue: true, Value: string value } &&
                   !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }
}
