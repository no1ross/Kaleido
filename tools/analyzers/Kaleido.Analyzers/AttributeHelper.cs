using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kaleido.Analyzers;

internal static class AttributeHelper
{
    public static bool HasNonEmptyNamedArgument(
        SeparatedSyntaxList<AttributeArgumentSyntax> args,
        string parameterName)
    {
        foreach (var arg in args)
        {
            if (arg.NameEquals?.Name.Identifier.Text != parameterName)
            {
                continue;
            }

            if (arg.Expression is LiteralExpressionSyntax literal &&
                !string.IsNullOrWhiteSpace(literal.Token.ValueText))
            {
                return true;
            }

            return false;
        }

        return false;
    }
}
