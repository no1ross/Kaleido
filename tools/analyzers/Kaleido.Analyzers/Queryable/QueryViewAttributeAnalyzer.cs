using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Queryable;

/// <summary>
/// KAL2003 — [QueryView] must declare a non-empty Name and Version.
/// A query view with an empty Name or Version cannot be registered by the framework.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QueryViewAttributeAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeFullName = "Kaleido.Queryable.QueryViewAttribute";

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.QueryViewAttributeValidity,
            "[QueryView] Name and Version must be non-empty",
            "[QueryView] '{0}' must have a non-empty Name and Version",
            "Kaleido.Usage",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "A [QueryView]-annotated class with an empty Name or Version cannot be registered by the Kaleido runtime.");

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

        if (ctorSymbol.ContainingType.ToDisplayString() != AttributeFullName)
        {
            return;
        }

        var args = attributeSyntax.ArgumentList?.Arguments ?? default;

        if (!AttributeHelper.HasNonEmptyNamedArgument(args, "Name") ||
            !AttributeHelper.HasNonEmptyNamedArgument(args, "Version"))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, attributeSyntax.GetLocation(), ctorSymbol.ContainingType.Name));
        }
    }
}
