using System.Linq;
using Microsoft.CodeAnalysis;

namespace Kaleido.Analyzers;

internal static class FixtureConventions
{
    public const string TestsSuffix = "Tests";

    /// <summary>
    /// A top-level class containing at least one [Fact]/[Theory] method.
    /// </summary>
    public static bool IsFixture(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Class &&
        type.ContainingType is null &&
        type.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(m =>
                m.MethodKind == MethodKind.Ordinary &&
                m.GetAttributes().Any(a =>
                    a.AttributeClass?.ToDisplayString() is
                        "Xunit.FactAttribute" or "Xunit.TheoryAttribute"));

    /// <summary>
    /// Resolves a fixture's name prefix ({Sut}Tests → {Sut}) to a type in the
    /// compilation or its references. Returns null when the fixture isn't
    /// named with the Tests suffix or no type matches the prefix.
    /// </summary>
    public static INamedTypeSymbol? ResolveSut(
        Compilation compilation,
        string fixtureName,
        System.Threading.CancellationToken cancellationToken)
    {
        if (!fixtureName.EndsWith(TestsSuffix, System.StringComparison.Ordinal))
        {
            return null;
        }

        var prefix = fixtureName.Substring(0, fixtureName.Length - TestsSuffix.Length);

        if (prefix.Length == 0)
        {
            return null;
        }

        return compilation
            .GetSymbolsWithName(prefix, SymbolFilter.Type, cancellationToken)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault(s => s.TypeKind != TypeKind.Error);
    }
}
