using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// KAL1005 — tests/AGENTS.md: any test-built ServiceProvider must use
/// BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true,
/// ValidateOnBuild = true }) to catch captive dependencies at build time.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BuildServiceProviderAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.BuildServiceProviderOptions,
            "Build test service providers with ValidateScopes + ValidateOnBuild",
            "BuildServiceProvider() must pass 'new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }'",
            "Kaleido.Tests",
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

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                is not IMethodSymbol method ||
            method.Name != "BuildServiceProvider" ||
            method.ContainingType?.ToDisplayString() !=
                "Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions")
        {
            return;
        }

        if (HasValidatedOptions(invocation))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool HasValidatedOptions(InvocationExpressionSyntax invocation)
    {
        var argument = invocation.ArgumentList.Arguments.FirstOrDefault();

        if (argument?.Expression is not ObjectCreationExpressionSyntax options ||
            options.Initializer is null)
        {
            return false;
        }

        var enabled =
            options.Initializer.Expressions
                .OfType<AssignmentExpressionSyntax>()
                .Where(a =>
                    a.Left is IdentifierNameSyntax id &&
                    (id.Identifier.ValueText == "ValidateScopes" ||
                     id.Identifier.ValueText == "ValidateOnBuild") &&
                    a.Right is LiteralExpressionSyntax lit &&
                    lit.IsKind(SyntaxKind.TrueLiteralExpression))
                .Select(a => ((IdentifierNameSyntax)a.Left).Identifier.ValueText)
                .ToList();

        return enabled.Contains("ValidateScopes") && enabled.Contains("ValidateOnBuild");
    }
}
