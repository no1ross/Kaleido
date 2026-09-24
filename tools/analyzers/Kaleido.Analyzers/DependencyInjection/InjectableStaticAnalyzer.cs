using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.DependencyInjection;

/// <summary>
/// KAL0005 — service-like types must not be static. A static class named like
/// an injectable component (*Service, *Handler, *Executor, *Processor,
/// *Provider, *Factory, *Reader, *Writer) declaring non-extension methods is
/// stateful behavior hiding in a static shape — make it an injectable type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InjectableStaticAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] ServiceSuffixes =
        ["Service", "Handler", "Executor", "Processor", "Provider", "Factory", "Reader", "Writer"];

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.InjectableStatic,
            "Service-like types must not be static",
            "Static class '{0}' looks like an injectable service ('{1}' is not an extension method) — make it injectable or rename it",
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

        if (type.TypeKind != TypeKind.Class || !type.IsStatic ||
            !ServiceSuffixes.Any(s => type.Name.EndsWith(s, System.StringComparison.Ordinal)))
        {
            return;
        }

        var offender =
            type.GetMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m =>
                    m.MethodKind == MethodKind.Ordinary &&
                    m.IsStatic &&
                    !m.IsExtensionMethod &&
                    m.DeclaredAccessibility != Accessibility.Private);

        if (offender is null)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, type.Locations[0], type.Name, offender.Name));
    }
}
