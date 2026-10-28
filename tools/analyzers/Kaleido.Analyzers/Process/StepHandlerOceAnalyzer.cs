using System.Collections.Immutable;
using System.Linq;
using Kaleido.Analyzers.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Process;

/// <summary>
/// KAL2004 — step handler catch (Exception) must filter OperationCanceledException.
/// A bare catch (Exception) in a step handler's ExecuteAsync body will silently swallow
/// cancellations, causing false failures in observability and masking client disconnects.
/// Add a when (ex is not OperationCanceledException) filter or a separate catch block.
/// Whether a filter excludes cancellation is proven from its semantics, not its text
/// (<see cref="CancellationCatch"/>, shared with KAL0018).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StepHandlerOceAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.StepHandlerOceMissing,
            "Step handler catch (Exception) must filter OperationCanceledException",
            "Step handler '{0}' has a catch (Exception) block that does not filter OperationCanceledException — add 'when (ex is not OperationCanceledException)' or a preceding 'catch (OperationCanceledException)'",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinks.For(DiagnosticIds.StepHandlerOceMissing),
            description: "Swallowing OperationCanceledException in a step handler inflates error metrics and hides client disconnects. Filter it explicitly.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Kaleido's handler interfaces, by metadata name (#465): an unrelated interface that happens to
        // be called IProcessStepHandler is not a step handler.
        context.RegisterCompilationStartAction(start =>
        {
            var handlers = new[]
                {
                    start.Compilation.GetTypeByMetadataName("Kaleido.Processor.Execution.IProcessStepHandler`1"),
                    start.Compilation.GetTypeByMetadataName("Kaleido.Processor.Execution.IProcessStepHandler`2"),
                }
                .Where(x => x is not null)
                .Cast<INamedTypeSymbol>()
                .ToImmutableArray();

            if (handlers.Length > 0)
            {
                start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, handlers), SyntaxKind.MethodDeclaration);
            }
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> handlers)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        if (method.Body is null ||
            context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is not IMethodSymbol methodSymbol ||
            !ImplementsHandlerExecute(methodSymbol, handlers))
        {
            return;
        }

        foreach (var catchClause in method.Body.DescendantNodes().OfType<CatchClauseSyntax>())
        {
            if (CancellationCatch.SeesCancellationAsFailure(catchClause, context.SemanticModel, context.CancellationToken))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, catchClause.GetLocation(), methodSymbol.ContainingType.Name));
            }
        }
    }

    // The method is the containing type's implementation of a Kaleido handler interface's ExecuteAsync
    // (implicit or explicit), not just any method of that name.
    private static bool ImplementsHandlerExecute(IMethodSymbol method, ImmutableArray<INamedTypeSymbol> handlers) =>
        method.ContainingType.AllInterfaces
            .Where(x => handlers.Contains(x.OriginalDefinition, SymbolEqualityComparer.Default))
            .SelectMany(x => x.GetMembers("ExecuteAsync").OfType<IMethodSymbol>())
            .Any(x => SymbolEqualityComparer.Default.Equals(method.ContainingType.FindImplementationForInterfaceMember(x), method));
}
