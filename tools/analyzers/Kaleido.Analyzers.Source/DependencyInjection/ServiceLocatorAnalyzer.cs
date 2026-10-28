using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Source.DependencyInjection;

/// <summary>
/// KAL0006 — no service locator. Resolving IServiceProvider.GetService /
/// GetServices / GetRequiredService outside composition roots hides
/// dependencies; constructor injection makes them explicit.
/// *ServiceCollectionExtensions classes are exempt — registration factories
/// legitimately resolve services there. Resolution from a scope created in the
/// same method (<c>var scope = factory.CreateScope(); scope.ServiceProvider.Get…</c>)
/// is exempt too — a child scope's services can never be constructor-injected.
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
            DiagnosticSeverity.Error,
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

        if (IsFromScopeCreatedHere(invocation, context))
        {
            return;
        }

        // A keyed resolution whose key is only known at runtime (a client name chosen by the caller) is
        // the container's dispatch seam, like a runtime Type (#498): no constructor can declare it. A
        // constant key could be injected with [FromKeyedServices] and is still reported.
        if (method.Name is "GetKeyedService" or "GetRequiredKeyedService" &&
            KeyArgument(invocation, method) is { } key &&
            !context.SemanticModel.GetConstantValue(key, context.CancellationToken).HasValue)
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

    // The serviceKey argument of a keyed resolution: the first argument of the reduced extension call
    // (provider.GetRequiredKeyedService<T>(key)), the second of the static form.
    private static ExpressionSyntax? KeyArgument(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        var arguments = invocation.ArgumentList.Arguments;
        var index = method.ReducedFrom is not null ? 0 : 1;

        return arguments.FirstOrDefault(x => x.NameColon?.Name.Identifier.ValueText == "serviceKey")?.Expression
            ?? (arguments.Count > index ? arguments[index].Expression : null);
    }

    // scope.ServiceProvider.Get…() where `scope` is a local initialized by
    // CreateScope()/CreateAsyncScope() — the child scope is the composition seam.
    private static bool IsFromScopeCreatedHere(
        InvocationExpressionSyntax invocation,
        SyntaxNodeAnalysisContext context)
    {
        var target =
            invocation.Expression is MemberAccessExpressionSyntax call
                ? call.Expression
                : null;

        if (target is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ServiceProvider" } providerAccess ||
            context.SemanticModel.GetSymbolInfo(providerAccess.Expression, context.CancellationToken).Symbol
                is not ILocalSymbol scopeLocal)
        {
            return false;
        }

        return scopeLocal.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(context.CancellationToken))
            .OfType<VariableDeclaratorSyntax>()
            .Any(declarator =>
                declarator.Initializer?.Value is InvocationExpressionSyntax creation &&
                context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol
                    is IMethodSymbol { Name: "CreateScope" or "CreateAsyncScope" });
    }
}
