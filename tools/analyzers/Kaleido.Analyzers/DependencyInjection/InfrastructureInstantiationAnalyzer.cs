using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.DependencyInjection;

/// <summary>
/// KAL0012 — do not manually instantiate infrastructure dependencies.
/// new HttpClient(), new ServiceCollection(), new LoggerFactory(), or new-ing
/// a DbContext bypasses the factories and lifetimes the container manages
/// (IHttpClientFactory, ILogger&lt;T&gt;, IServiceScopeFactory,
/// IDbContextFactory&lt;T&gt;). *ServiceCollectionExtensions classes are
/// exempt — bootstrapping legitimately happens there.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InfrastructureInstantiationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.InfrastructureInstantiation,
            "Do not manually instantiate infrastructure dependencies",
            "'new {0}()' bypasses the container-managed factory — inject the corresponding abstraction instead",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (ServiceConventions.IsInsideCompositionRoot(context))
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(context.Node, context.CancellationToken)
                .Type is not INamedTypeSymbol type)
        {
            return;
        }

        var isInfrastructure =
            type.ToDisplayString() is
                "System.Net.Http.HttpClient" or
                "Microsoft.Extensions.DependencyInjection.ServiceCollection" or
                "Microsoft.Extensions.DependencyInjection.ServiceProvider" or
                "Microsoft.Extensions.Logging.LoggerFactory" ||
            IsDbContext(type);

        if (!isInfrastructure)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, context.Node.GetLocation(), type.Name));
    }

    private static bool IsDbContext(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == "Microsoft.EntityFrameworkCore.DbContext")
            {
                return true;
            }
        }

        return false;
    }
}
