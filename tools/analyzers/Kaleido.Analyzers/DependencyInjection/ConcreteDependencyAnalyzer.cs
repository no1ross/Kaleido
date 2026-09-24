using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// KAL0008 — inject abstractions, not concrete service implementations. A
/// constructor parameter typed as a concrete class that was registered under
/// an I* service key hard-codes the implementation and defeats the seam.
/// Types registered as themselves (concrete key) are exempt — injecting them
/// concretely is the intended usage.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConcreteDependencyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.ConcreteDependency,
            "Inject the service abstraction, not the concrete implementation",
            "Parameter '{0}' is typed as concrete '{1}' — inject '{2}' instead",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        var registrations =
            ServiceConventions.Harvest(
                context.Compilation, context.CancellationToken);

        if (registrations.ServiceToImpl.Count == 0)
        {
            return;
        }

        context.RegisterSymbolAction(
            symbolContext => AnalyzeMethod(symbolContext, registrations),
            SymbolKind.Method);
    }

    private static void AnalyzeMethod(
        SymbolAnalysisContext context,
        ServiceRegistrationModel registrations)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (method.MethodKind != MethodKind.Constructor)
        {
            return;
        }

        foreach (var parameter in method.Parameters)
        {
            if (parameter.Type is not INamedTypeSymbol paramType ||
                paramType.TypeKind != TypeKind.Class)
            {
                continue;
            }

            var abstraction =
                registrations.ServiceToImpl
                    .FirstOrDefault(pair =>
                        pair.Key.TypeKind == TypeKind.Interface &&
                        SymbolEqualityComparer.Default.Equals(
                            pair.Value, paramType.OriginalDefinition));

            if (abstraction.Key is null)
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Rule,
                    parameter.Locations[0],
                    parameter.Name,
                    paramType.Name,
                    abstraction.Key.Name));
        }
    }
}
