using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Source.Design;

/// <summary>
/// KAL0001 - static is fine for mechanics; static is not where behavior lives (#459).
/// </summary>
/// <remarks>
/// <para>
/// Meaningful runtime behavior belongs in injectable concrete types with explicit dependencies (an
/// interface only where there is a real abstraction boundary), not hidden in a static class. A static
/// class is accepted when it holds extension methods, or stateless value mechanics: names, routes,
/// URLs, formats - methods that only turn simple values into simple values.
/// </para>
/// <para>
/// Reported: mutable static state (a field that is neither <c>const</c> nor <c>readonly</c>, a settable
/// static property, a static event), and a non-private, non-extension method that takes or returns
/// anything but simple values (primitives, <c>string</c>, <c>char</c>, <c>Guid</c>, enums, dates and
/// times, <c>Uri</c>, <c>ReadOnlySpan&lt;char&gt;</c>, their nullable forms, and arrays or read-only
/// sequences of them) - a runtime or domain type, a service, a type parameter, or nothing (<c>void</c>).
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StaticClassExtensionsAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.StaticClassExtensions,
            "Static class holds behavior or state",
            "Static class '{0}' {1} - static classes are for extension methods and stateless value mechanics (names, routes, URLs, formats); behavior belongs in an injectable concrete type, or in an extension method of the type it works on",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    private static readonly ImmutableHashSet<string> SimpleValueTypes =
        ImmutableHashSet.Create(
            "System.Guid",
            "System.DateTimeOffset",
            "System.TimeSpan",
            "System.DateOnly",
            "System.TimeOnly",
            "System.Uri");

    private static readonly ImmutableHashSet<string> SimpleSequences =
        ImmutableHashSet.Create(
            "System.Collections.Generic.IEnumerable`1",
            "System.Collections.Generic.IReadOnlyCollection`1",
            "System.Collections.Generic.IReadOnlyList`1",
            "System.ReadOnlySpan`1");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class || !type.IsStatic)
        {
            return;
        }

        if (Finding(type) is { } finding)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations[0], type.Name, finding));
        }
    }

    private static string? Finding(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers().Where(x => !x.IsImplicitlyDeclared))
        {
            switch (member)
            {
                case IFieldSymbol { IsConst: false, IsReadOnly: false } field:
                    return $"holds mutable state in '{field.Name}'";
                case IPropertySymbol { SetMethod: not null } property:
                    return $"holds mutable state in '{property.Name}'";
                case IEventSymbol @event:
                    return $"holds mutable state in '{@event.Name}'";
                case IMethodSymbol { MethodKind: MethodKind.Ordinary, IsExtensionMethod: false } method
                    when method.DeclaredAccessibility != Accessibility.Private && !IsValueMechanics(method):
                    return $"declares '{method.Name}', which works on more than simple values";
            }
        }

        return null;
    }

    private static bool IsValueMechanics(IMethodSymbol method) =>
        !method.IsGenericMethod &&
        !method.ReturnsVoid &&
        IsSimple(method.ReturnType) &&
        method.Parameters.All(x => IsSimple(x.Type));

    private static bool IsSimple(ITypeSymbol type) =>
        type switch
        {
            { SpecialType: >= SpecialType.System_Boolean and <= SpecialType.System_String } => true,
            { SpecialType: SpecialType.System_DateTime } => true,
            { TypeKind: TypeKind.Enum } => true,
            IArrayTypeSymbol array => IsSimple(array.ElementType),
            INamedTypeSymbol { IsGenericType: true } generic
                when generic.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                    || SimpleSequences.Contains(FullName(generic))
                => generic.TypeArguments.All(IsSimple),
            INamedTypeSymbol named => SimpleValueTypes.Contains(FullName(named)),
            _ => false,
        };

    // Namespace and metadata name: unaffected by nullable annotations and type arguments.
    private static string FullName(INamedTypeSymbol type) =>
        $"{type.ContainingNamespace.ToDisplayString()}.{type.MetadataName}";
}
