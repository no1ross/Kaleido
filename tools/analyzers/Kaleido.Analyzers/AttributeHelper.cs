using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kaleido.Analyzers;

internal static class AttributeHelper
{
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
