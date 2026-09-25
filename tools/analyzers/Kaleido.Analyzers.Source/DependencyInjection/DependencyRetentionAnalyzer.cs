using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Source.DependencyInjection;

/// <summary>
/// KAL0010 — injected dependency fields must be readonly. A mutable field
/// typed as a registered service can be reassigned after construction,
/// hiding state changes and defeating the immutability contract of DI.
/// Unused injected parameters are already caught by IDE0060.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DependencyRetentionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor MutableFieldRule =
        new(
            DiagnosticIds.DependencyRetention,
            "Injected dependency fields must be readonly",
            "Field '{0}' on service '{1}' holds '{2}' — make it readonly",
            "Kaleido.Design",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MutableFieldRule);

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

        if (registrations.Implementations.Count == 0)
        {
            return;
        }

        context.RegisterSymbolAction(
            symbolContext => AnalyzeType(symbolContext, registrations),
            SymbolKind.NamedType);
    }

    private static void AnalyzeType(
        SymbolAnalysisContext context,
        ServiceRegistrationModel registrations)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class ||
            !registrations.Implementations.Contains(type.OriginalDefinition))
        {
            return;
        }

        var services = registrations.Services.ToList();

        var isServiceType = (ITypeSymbol t) =>
            t is INamedTypeSymbol named &&
            services.Any(s => SymbolEqualityComparer.Default.Equals(
                s, named.OriginalDefinition));

        foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.IsReadOnly || field.IsImplicitlyDeclared || field.IsConst ||
                !isServiceType(field.Type))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    MutableFieldRule,
                    field.Locations[0],
                    field.Name,
                    type.Name,
                    field.Type.Name));
        }
    }
}
