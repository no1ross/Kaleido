using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Process;

/// <summary>
/// KAL2001 — [ProcessStep] must declare a non-empty Name and Version.
/// A step with an empty Name or Version cannot be registered by the framework.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProcessStepAttributeAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeFullName = "Kaleido.Processor.ProcessStepAttribute";

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.ProcessStepAttributeValidity,
            "[ProcessStep] Name and Version must be non-empty",
            "[ProcessStep] '{0}' must have a non-empty Name and Version",
            "Kaleido.Usage",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "A [ProcessStep]-annotated class with an empty Name or Version cannot be registered by the Kaleido runtime.");

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
