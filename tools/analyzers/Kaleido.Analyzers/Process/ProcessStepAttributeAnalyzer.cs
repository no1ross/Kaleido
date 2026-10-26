using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Process;

/// <summary>
/// KAL2001 — [ProcessStep] must declare a non-empty DisplayName and Description.
/// A step with either of them empty fails startup registration; DisplayName and Description
/// are published through the registry for UIs, documentation, and AI agents.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProcessStepAttributeAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeFullName = "Kaleido.Processor.ProcessStepAttribute";

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.ProcessStepAttributeValidity,
            "[ProcessStep] DisplayName and Description must be non-empty",
            "[ProcessStep] '{0}' must have a non-empty DisplayName and Description",
            "Kaleido.Usage",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinks.For(DiagnosticIds.ProcessStepAttributeValidity),
            description: "A [ProcessStep] with an empty DisplayName or Description cannot be registered by the Kaleido runtime.");

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

        if (!AttributeHelper.HasNonEmptyNamedArgument(args, "DisplayName", context.SemanticModel) ||
            !AttributeHelper.HasNonEmptyNamedArgument(args, "Description", context.SemanticModel))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, attributeSyntax.GetLocation(), ctorSymbol.ContainingType.Name));
        }
    }
}
