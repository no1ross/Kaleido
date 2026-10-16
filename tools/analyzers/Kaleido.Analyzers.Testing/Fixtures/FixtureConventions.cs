using System.Linq;
using Microsoft.CodeAnalysis;

namespace Kaleido.Analyzers.Testing.Fixtures;

internal static class FixtureConventions
{
    public const string TestsSuffix = "Tests";

    /// <summary>
    /// True when the assembly being compiled ends with ".UnitTests".
    /// Functional, integration, and analyzer-harness test projects are excluded
    /// from SUT-naming rules — they are scenario-scoped, not SUT-scoped.
    /// </summary>
    public static bool IsUnitTestAssembly(Compilation compilation) =>
        compilation.AssemblyName?.EndsWith(".UnitTests", System.StringComparison.Ordinal) == true;

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
    /// compilation or its references. References are searched with the
    /// source assembly the unit-test assembly is named after first, and only
    /// types accessible to the fixture's assembly match. Returns null when the
    /// fixture isn't named with the Tests suffix or no type matches the prefix.
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

        var sourceAssemblyName = SourceAssemblyName(compilation);

        var assemblies =
            compilation.References
                .Select(compilation.GetAssemblyOrModuleSymbol)
                .OfType<IAssemblySymbol>()
                .OrderBy(a => a.Name == sourceAssemblyName ? 0 : 1);

        foreach (var assembly in assemblies)
        {
            var hit = FindType(compilation, assembly.GlobalNamespace, prefix);

            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

    /// <summary>
    /// The source assembly a unit-test assembly is named after
    /// (Kaleido.Http.UnitTests → Kaleido.Http); null for any other assembly.
    /// </summary>
    private static string? SourceAssemblyName(Compilation compilation) =>
        IsUnitTestAssembly(compilation)
            ? compilation.AssemblyName!.Substring(0, compilation.AssemblyName.Length - ".UnitTests".Length)
            : null;

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

    /// <summary>
    /// True when the type is a "testable" framework class: public or internal,
    /// non-static, non-abstract, with behavior (at least one ordinary method).
    /// DTOs, records, exceptions, attributes, and [ExcludeFromCodeCoverage]
    /// types are exempt — new-ing those in a fixture is arrangement, not a
    /// collaborator violation.
    /// </summary>
    public static bool IsTestable(INamedTypeSymbol type)
    {
        // CanBeReferencedByName: compiler-generated types (e.g. <PrivateImplementationDetails>)
        // read from a compiled reference are not flagged IsImplicitlyDeclared.
        if (type.TypeKind != TypeKind.Class ||
            type.IsAbstract ||
            type.IsStatic ||
            type.IsImplicitlyDeclared ||
            !type.CanBeReferencedByName ||
            type.IsRecord ||
            type.ContainingType is not null ||
            type.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
        {
            return false;
        }

        if (InheritsFrom(type, "System.Exception") ||
            InheritsFrom(type, "System.Attribute"))
        {
            return false;
        }

        if (DtoSuffixes.Any(s => type.Name.EndsWith(s, System.StringComparison.Ordinal)))
        {
            return false;
        }

        if (type.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() ==
                "System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute"))
        {
            return false;
        }

        return type.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(m =>
                m.MethodKind == MethodKind.Ordinary &&
                !m.IsImplicitlyDeclared);
    }

    private static readonly string[] DtoSuffixes =
        ["Options", "Settings", "Request", "Response", "Metadata", "Details", "Parameters", "Envelope", "Descriptor", "Summary", "Item", "Message", "Event"];

    private static bool InheritsFrom(INamedTypeSymbol type, string baseTypeName)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == baseTypeName)
            {
                return true;
            }
        }

        return false;
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

    /// <summary>
    /// The first type named <paramref name="name"/> the fixture's assembly can
    /// see: an internal type of a third-party reference (e.g. Castle's
    /// TypeExtensions, reached through Moq) cannot be a subject under test.
    /// </summary>
    private static INamedTypeSymbol? FindType(
        Compilation compilation,
        INamespaceSymbol ns,
        string name)
    {
        var member =
            ns.GetTypeMembers(name)
                .FirstOrDefault(t => compilation.IsSymbolAccessibleWithin(t, compilation.Assembly));

        if (member is not null)
        {
            return member;
        }

        foreach (var hit in ns.GetNamespaceMembers().Select(child => FindType(compilation, child, name)))
        {
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }
}
