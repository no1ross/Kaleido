using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Queryable;

/// <summary>
/// KAL2002 — [QuerySource] must declare a non-empty Version, DisplayName, and Description.
/// A query source with any of them empty fails startup registration; DisplayName and
/// Description are published through the registry for UIs, documentation, and AI agents.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QuerySourceAttributeAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.QuerySourceAttributeValidity,
            "[QuerySource] Version, DisplayName, and Description must be non-empty",
            "[QuerySource] '{0}' must have a non-empty Version, DisplayName, and Description",
            "Kaleido.Usage",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "A [QuerySource] with an empty Version, DisplayName, or Description cannot be registered by the Kaleido runtime.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Attribute);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var attributeSyntax = (AttributeSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(attributeSyntax, context.CancellationToken)
                .Symbol is not IMethodSymbol ctorSymbol)
        {
            return;
        }

        if (ctorSymbol.ContainingType.ToDisplayString() != QueryableSymbols.SourceAttributeFullName)
        {
            return;
        }

        var args = attributeSyntax.ArgumentList?.Arguments ?? default;

        if (!AttributeHelper.HasNonEmptyNamedArgument(args, "Version") ||
            !AttributeHelper.HasNonEmptyNamedArgument(args, "DisplayName") ||
            !AttributeHelper.HasNonEmptyNamedArgument(args, "Description"))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, attributeSyntax.GetLocation(), ctorSymbol.ContainingType.Name));
        }
    }
}
