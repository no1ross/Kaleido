using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// KAL0003 — the ! null-forgiving operator hides nullability assumptions.
/// AGENTS.md: replace with explicit null checks that throw Kaleido exceptions.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullForgivingOperatorAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.NullForgivingOperator,
            "Do not use the null-forgiving operator",
            "Replace '!' with an explicit null check",
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
            SyntaxKind.SuppressNullableWarningExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        // Report on the '!' operator token itself, not the whole operand!
        context.ReportDiagnostic(
            Diagnostic.Create(Rule, context.Node.GetLastToken().GetLocation()));
    }
}
