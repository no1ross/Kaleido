using Microsoft.CodeAnalysis;

namespace Kaleido.Analyzers.Source;

internal static class SymbolExtensions
{
    public static bool DerivesFrom(
        this INamedTypeSymbol type,
        string baseTypeMetadataName)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == baseTypeMetadataName)
            {
                return true;
            }
        }

        return false;
    }
}
