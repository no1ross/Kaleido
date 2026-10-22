using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kaleido.Analyzers.Shared;

/// <summary>
/// Shared by KAL0019/KAL0020 (Kaleido.Analyzers.Source) and KAL1009/KAL1002/KAL1003/KAL1007
/// (Kaleido.Analyzers.Testing): which types have behavior - and so need their own file and their own
/// unit-test class - and the name that file and test class carry (#404).
/// </summary>
/// <remarks>
/// <para>
/// A type has behavior when it declares a member written by hand: an ordinary method, a user-defined
/// operator or conversion, or a property accessor with a body (explicit interface implementations
/// included). Members the compiler writes - auto-property accessors, positional record members, record
/// <c>Equals</c>/<c>ToString</c>/<c>&lt;Clone&gt;$</c> - and source-generated members (<c>[LoggerMessage]</c>)
/// do not count. Constructors are not counted: they are not visible as behavior from metadata.
/// </para>
/// <para>
/// Candidates are top-level, non-abstract, non-static public or internal classes, records and structs.
/// Exceptions, attributes and EF Core model configuration (<c>IEntityTypeConfiguration&lt;T&gt;</c>,
/// <c>DbContext</c>) are exempt; the latter is covered end to end by the store's tests.
/// </para>
/// <para>
/// Works on source symbols (the file rule) and metadata symbols (the fixture rules read <c>src/</c> as
/// referenced assemblies): in source an accessor counts when its syntax has a body, from metadata when
/// it is not <c>[CompilerGenerated]</c>.
/// </para>
/// </remarks>
internal static class TypeBehavior
{
    private const string CompilerGenerated = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";
    private const string GeneratedCode = "System.CodeDom.Compiler.GeneratedCodeAttribute";

    /// <summary>True when the type needs its own file and its own unit-test class.</summary>
    public static bool HasBehavior(INamedTypeSymbol type) =>
        IsCandidate(type) && type.GetMembers().OfType<IMethodSymbol>().Any(IsHandWritten);

    /// <summary>
    /// The name of the type's file and test class: the type name, or - for a generic type with a
    /// same-name, lower-arity twin in its namespace - the name with its arity:
    /// <c>ProcessStepHandlerResult&lt;T&gt;</c> is <c>ProcessStepHandlerResultOfT</c>, a two-parameter twin
    /// <c>…OfT2</c>.
    /// </summary>
    public static string ConventionName(INamedTypeSymbol type)
    {
        if (type.Arity == 0 || type.ContainingType is not null)
        {
            return type.Name;
        }

        var hasLowerArityTwin =
            type.ContainingNamespace
                .GetTypeMembers(type.Name)
                .Any(t =>
                    t.Arity < type.Arity &&
                    SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, type.ContainingAssembly));

        if (!hasLowerArityTwin)
        {
            return type.Name;
        }

        return type.Arity == 1 ? type.Name + "OfT" : type.Name + "OfT" + type.Arity.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Splits a convention name back into the type name and arity it stands for: <c>XOfT</c> is
    /// (<c>X</c>, 1), <c>XOfT3</c> is (<c>X</c>, 3); any other name is itself with no arity constraint.
    /// </summary>
    public static (string Name, int? Arity) ParseConventionName(string conventionName)
    {
        var index = conventionName.LastIndexOf("OfT", System.StringComparison.Ordinal);

        if (index <= 0)
        {
            return (conventionName, null);
        }

        var digits = conventionName.Substring(index + 3);

        if (digits.Length == 0)
        {
            return (conventionName.Substring(0, index), 1);
        }

        return int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var arity) && arity > 1
            ? (conventionName.Substring(0, index), arity)
            : (conventionName, null);
    }

    private static bool IsCandidate(INamedTypeSymbol type) =>
        type.TypeKind is TypeKind.Class or TypeKind.Struct &&
        !type.IsAbstract &&
        !type.IsStatic &&
        !type.IsImplicitlyDeclared &&
        type.CanBeReferencedByName &&
        type.ContainingType is null &&
        type.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal &&
        !InheritsFrom(type, "System.Exception") &&
        !InheritsFrom(type, "System.Attribute") &&
        !InheritsFrom(type, "Microsoft.EntityFrameworkCore.DbContext") &&
        !type.AllInterfaces.Any(i => i.OriginalDefinition.ToDisplayString() == "Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<TEntity>");

    private static bool IsHandWritten(IMethodSymbol method)
    {
        if (method.IsImplicitlyDeclared ||
            method.IsAbstract ||
            method.MethodKind is not (MethodKind.Ordinary or MethodKind.UserDefinedOperator or MethodKind.Conversion
                or MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.ExplicitInterfaceImplementation) ||
            HasAttribute(method, CompilerGenerated) ||
            HasAttribute(method, GeneratedCode))
        {
            return false;
        }

        // A partial method whose implementation a generator writes ([LoggerMessage]).
        if (method.IsPartialDefinition &&
            (method.PartialImplementationPart is not { } implementation || HasAttribute(implementation, GeneratedCode) || IsInGeneratedFile(implementation)))
        {
            return false;
        }

        if (!method.Locations.Any(l => l.IsInSource))
        {
            return true; // metadata: compiler-generated members were excluded above
        }

        if (method.MethodKind is not (MethodKind.PropertyGet or MethodKind.PropertySet))
        {
            return method.DeclaringSyntaxReferences.Length > 0;
        }

        // Source accessors: an auto accessor and a positional record parameter have no body.
        return method.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() switch
        {
            AccessorDeclarationSyntax accessor => accessor.Body is not null || accessor.ExpressionBody is not null,
            ArrowExpressionClauseSyntax => true,
            _ => false,
        });
    }

    private static bool IsInGeneratedFile(ISymbol symbol) =>
        symbol.Locations.Any(l => l.SourceTree?.FilePath is { } path && IsGeneratedFilePath(path));

    /// <summary>A file a source generator or the build writes (obj/, *.g.cs, *.generated.cs).</summary>
    public static bool IsGeneratedFilePath(string path) =>
        path.EndsWith(".g.cs", System.StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".generated.cs", System.StringComparison.OrdinalIgnoreCase) ||
        path.Replace('\\', '/').IndexOf("/obj/", System.StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool HasAttribute(ISymbol symbol, string attributeName) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == attributeName);

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
}
