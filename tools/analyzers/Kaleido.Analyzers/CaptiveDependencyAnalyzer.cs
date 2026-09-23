using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// KAL0014 — singleton registrations must not capture scoped services.
/// Resolving a scoped service inside an AddSingleton factory lambda creates
/// a captive dependency that lives forever. Flagged only when both lifetimes
/// are known from registrations in the same compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CaptiveDependencyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.CaptiveDependency,
            "Singleton registrations must not resolve scoped services",
            "Singleton factory resolves '{0}' which is registered as Scoped — the scoped instance would be captured for the app lifetime",
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

        var scoped =
            registrations.Lifetimes
                .Where(pair => pair.Value == ServiceRegistrationModel.ServiceLifetime.Scoped)
                .Select(pair => pair.Key)
                .ToList();

        if (scoped.Count == 0)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeInvocation(nodeContext, scoped),
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        System.Collections.Generic.List<INamedTypeSymbol> scoped)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // only look inside singleton factory lambdas
        var registrationCall =
            invocation.Ancestors().OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(a =>
                    a.Expression is MemberAccessExpressionSyntax access &&
                    access.Name.Identifier.ValueText.Contains("Singleton"));

        if (registrationCall is null ||
            !invocation.Ancestors().OfType<AnonymousFunctionExpressionSyntax>().Any())
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken)
                .Symbol is not IMethodSymbol method ||
            method.Name is not ("GetService" or "GetServices" or "GetRequiredService" or
                                "GetKeyedService" or "GetRequiredKeyedService"))
        {
            return;
        }

        var resolved =
            method.TypeArguments.FirstOrDefault() ??
            (invocation.DescendantNodes()
                .OfType<TypeOfExpressionSyntax>()
                .Select(t => context.SemanticModel.GetTypeInfo(
                    t.Type, context.CancellationToken).Type)
                .OfType<INamedTypeSymbol>()
                .FirstOrDefault());

        if (resolved is not INamedTypeSymbol service ||
            !scoped.Any(s => SymbolEqualityComparer.Default.Equals(
                s, service.OriginalDefinition)))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                invocation.GetLocation(),
                service.Name));
    }
}
