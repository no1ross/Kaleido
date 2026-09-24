using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// KAL0007 — no service locator. Resolving IServiceProvider.GetService /
/// GetServices / GetRequiredService outside composition roots hides
/// dependencies; constructor injection makes them explicit.
/// *ServiceCollectionExtensions classes are exempt — registration factories
/// legitimately resolve services there.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServiceLocatorAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.ServiceLocator,
            "Resolve dependencies through constructor injection, not IServiceProvider",
            "'{0}' on IServiceProvider is a service locator call — inject the dependency through the constructor instead",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken)
                .Symbol is not IMethodSymbol method)
        {
            return;
        }

        var isServiceResolution =
            method.ContainingType?.ToDisplayString() is
                "System.IServiceProvider" or
                "Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions" or
                "Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions" &&
            method.Name is "GetService" or "GetServices" or "GetRequiredService" or
                             "GetKeyedService" or "GetRequiredKeyedService";

        if (!isServiceResolution || ServiceConventions.IsInsideCompositionRoot(context))
        {
            return;
        }

        // request-scoped resolution (context.RequestServices) is the
        // middleware/endpoint activation pattern — exempt
        if (invocation.Expression is MemberAccessExpressionSyntax access &&
            access.Expression is MemberAccessExpressionSyntax receiver &&
            receiver.Name.Identifier.ValueText == "RequestServices")
        {
            return;
        }

        // dynamic resolution is the container's dispatch seam — only flag
        // statically-closed service types that could be ctor-injected
        var resolvedType =
            method.TypeArguments.FirstOrDefault();

        if (resolvedType is null ||
            resolvedType is ITypeParameterSymbol ||
            (resolvedType is INamedTypeSymbol named &&
             named.TypeArguments.Any(t => t is ITypeParameterSymbol)))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, invocation.GetLocation(), method.Name));
    }
}
