using System.Collections.Immutable;
using System.Linq;
using Kaleido.Analyzers.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Source.Design;

/// <summary>
/// KAL0018 — a catch-all (<c>catch</c> / <c>catch (Exception)</c>) that calls an
/// observability member must not see <see cref="System.OperationCanceledException"/>:
/// either filter it out (<c>when (ex is not OperationCanceledException)</c>) or handle
/// it in an earlier <c>catch (OperationCanceledException)</c> clause. Otherwise a
/// cancellation is recorded as a failure, inflating error metrics and triggering false
/// alerts (AGENTS.md, "OperationCanceledException and observability"). Whether a filter
/// excludes cancellation is proven from its semantics, not from the type names it mentions
/// (<see cref="CancellationCatch"/>, shared with KAL2004).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ObservabilityCancellationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.ObservabilityCancellation,
            "Catch-all that records observability must exclude OperationCanceledException",
            "This catch-all calls '{0}' but does not exclude OperationCanceledException — add 'when (ex is not OperationCanceledException)' or an earlier catch (OperationCanceledException)",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCatch, SyntaxKind.CatchClause);
    }

    private static void AnalyzeCatch(SyntaxNodeAnalysisContext context)
    {
        var catchClause = (CatchClauseSyntax)context.Node;
        var model = context.SemanticModel;

        if (!CancellationCatch.SeesCancellationAsFailure(catchClause, model, context.CancellationToken))
        {
            return;
        }

        var observabilityCall =
            catchClause.Block
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(invocation => IsObservabilityCall(invocation, model, context));

        if (observabilityCall is null)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                catchClause.CatchKeyword.GetLocation(),
                observabilityCall.Expression.ToString()));
    }

    private static bool IsObservabilityCall(InvocationExpressionSyntax invocation, SemanticModel model, SyntaxNodeAnalysisContext context) =>
        model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method &&
        (method.ContainingType.Name.Contains("Observation") || method.ContainingType.Name.Contains("Observability"));
}
