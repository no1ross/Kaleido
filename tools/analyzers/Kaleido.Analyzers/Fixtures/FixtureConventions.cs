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

        var sourceHit =
            compilation
                .GetSymbolsWithName(prefix, SymbolFilter.Type, cancellationToken)
                .OfType<INamedTypeSymbol>()
                .FirstOrDefault(s => s.TypeKind != TypeKind.Error);

        if (sourceHit is not null)
        {
            return sourceHit;
        }

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
            {
                continue;
            }

            var hit = FindType(assembly.GlobalNamespace, prefix);

            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

    /// <summary>
    /// True when the type inherits SutFixture or SutFixture&lt;TSut&gt;
    /// anywhere in its base chain (matched by metadata name, any namespace).
    /// </summary>
    public static bool InheritsSutFixture(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == "SutFixture")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the TSut type argument when the type inherits SutFixture&lt;TSut&gt;
    /// anywhere in its base chain (matched by metadata name, any namespace).
    /// </summary>
    public static INamedTypeSymbol? GetSutFixtureSut(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == "SutFixture" &&
                current.TypeArguments.Length == 1 &&
                current.TypeArguments[0] is INamedTypeSymbol sut)
            {
                return sut;
            }
        }

        return null;
    }

    /// <summary>All named types in a namespace, recursively.</summary>
    public static System.Collections.Generic.IEnumerable<INamedTypeSymbol> EnumerateTypes(
        INamespaceSymbol ns)
    {
        foreach (var member in ns.GetTypeMembers())
        {
            yield return member;
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            foreach (var member in EnumerateTypes(child))
            {
                yield return member;
            }
        }
    }

    private static INamedTypeSymbol? FindType(
        INamespaceSymbol ns,
        string name)
    {
        foreach (var member in ns.GetTypeMembers(name))
        {
            return member;
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            var hit = FindType(child, name);

            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }
}
