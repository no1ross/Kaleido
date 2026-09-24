using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.DependencyInjection;

/// <summary>
/// KAL0006 — the framework resolves services from DI; newing up a type that is
/// registered as a service implementation bypasses the seam. The registered
/// implementation set is harvested from *ServiceCollectionExtensions classes
/// in the same compilation. *ServiceCollectionExtensions classes are exempt —
/// they are the registration seam itself.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServiceInstantiationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.ServiceInstantiation,
            "Resolve service types from DI instead of newing them up",
            "Type '{0}' is a DI-registered service implementation — resolve it from DI instead of newing it up",
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

        if (registrations.Implementations.Count == 0)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeNode(nodeContext, registrations.Implementations),
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
    }

    private static void AnalyzeNode(
        SyntaxNodeAnalysisContext context,
        HashSet<INamedTypeSymbol> implementations)
    {
        if (context.SemanticModel.GetTypeInfo(context.Node, context.CancellationToken)
                .Type is not INamedTypeSymbol type ||
            type.TypeKind != TypeKind.Class ||
            !implementations.Contains(type.OriginalDefinition) ||
            ServiceConventions.IsInsideCompositionRoot(context))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                context.Node.GetLocation(),
                type.Name));
    }
}
