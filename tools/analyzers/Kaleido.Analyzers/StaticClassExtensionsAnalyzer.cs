using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// KAL0001 — static classes are reserved for extension methods (AGENTS.md convention).
/// A static class declaring ordinary (non-extension) methods should either expose
/// extensions or be an instance type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StaticClassExtensionsAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.StaticClassExtensions,
            "Static class declares non-extension methods",
            "Static class '{0}' is reserved for extension methods — convert '{1}' to an extension method or use a non-static class",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

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

        var offenders =
            type.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m =>
                    m.MethodKind == MethodKind.Ordinary &&
                    m.IsStatic &&
                    !m.IsExtensionMethod &&
                    m.DeclaredAccessibility != Accessibility.Private)
                .ToList();

        if (offenders.Count == 0)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                type.Locations[0],
                type.Name,
                offenders[0].Name));
    }
}
