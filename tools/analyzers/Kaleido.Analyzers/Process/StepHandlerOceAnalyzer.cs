using System.Collections.Immutable;
using System.Linq;
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
            description: "Swallowing OperationCanceledException in a step handler inflates error metrics and hides client disconnects. Filter it explicitly.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // Only look at ExecuteAsync methods
        if (method.Identifier.Text != "ExecuteAsync")
        {
            return;
        }

        // Check that the containing type implements IProcessStepHandler<T> or IProcessStepHandler<T, TResult>
        if (context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken)
                is not IMethodSymbol methodSymbol)
        {
            return;
        }

        var containingType = methodSymbol.ContainingType;
        if (!ImplementsProcessStepHandler(containingType))
        {
            return;
        }

        // Walk catch clauses in the method body
        if (method.Body is null)
        {
            return;
        }

        var catchClauses = method.Body.DescendantNodes().OfType<CatchClauseSyntax>();

        foreach (var catchClause in catchClauses)
        {
            if (!IsBareExceptionCatch(catchClause, context.SemanticModel, context.CancellationToken))
            {
                continue;
            }

            // Is there a filter with OperationCanceledException?
            if (HasOceFilter(catchClause))
            {
                continue;
            }

            // Is there a preceding catch (OperationCanceledException) in the same try?
            if (PrecedingCatchHandlesOce(catchClause, context.SemanticModel, context.CancellationToken))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Rule,
                    catchClause.GetLocation(),
                    containingType.Name));
        }
    }

    private static bool ImplementsProcessStepHandler(INamedTypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.Name == "IProcessStepHandler" &&
                (iface.TypeArguments.Length == 1 || iface.TypeArguments.Length == 2))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBareExceptionCatch(
        CatchClauseSyntax catchClause,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (catchClause.Declaration is null)
        {
            // catch { } with no type — catches everything
            return true;
        }

        var catchType = semanticModel.GetTypeInfo(
            catchClause.Declaration.Type, cancellationToken).Type;

        if (catchType is null)
        {
            return false;
        }

        var fullName = catchType.ToDisplayString();
        return fullName is "System.Exception" or "Exception";
    }

    private static bool HasOceFilter(CatchClauseSyntax catchClause)
    {
        if (catchClause.Filter is null)
        {
            return false;
        }

        // Look for patterns like: when (ex is not OperationCanceledException)
        // or when (!(ex is OperationCanceledException))
        var filterText = catchClause.Filter.FilterExpression.ToString();
        return filterText.Contains("OperationCanceledException");
    }

    private static bool PrecedingCatchHandlesOce(
        CatchClauseSyntax catchClause,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (catchClause.Parent is not TryStatementSyntax tryStatement)
        {
            return false;
        }

        foreach (var sibling in tryStatement.Catches)
        {
            if (sibling == catchClause)
            {
                // Stop at our own catch — only preceding ones matter
                break;
            }

            if (sibling.Declaration is null)
            {
                continue;
            }

            var siblingType = semanticModel.GetTypeInfo(
                sibling.Declaration.Type, cancellationToken).Type;

            if (siblingType is null)
            {
                continue;
            }

            var siblingName = siblingType.ToDisplayString();
            if (siblingName is "System.OperationCanceledException" or "OperationCanceledException")
            {
                return true;
            }
        }

        return false;
    }
}
