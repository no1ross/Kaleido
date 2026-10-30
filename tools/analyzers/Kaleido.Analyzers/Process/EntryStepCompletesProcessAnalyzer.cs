using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Kaleido.Analyzers.Process;

/// <summary>
/// KAL2022 - a step handler returns <c>ProcessStepHandlerResult.Complete(...)</c> for a step with no
/// <c>[DependsOn&lt;T&gt;]</c> or <c>[AvailableAfter&lt;T&gt;]</c>. Such a step is an entry step, which
/// can start a process, so the call would end a process in its first step. Completing a process is
/// the end of its work, typically an internal-only step after others (ADR 0033, #534).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EntryStepCompletesProcessAnalyzer : DiagnosticAnalyzer
{
    private const string HandlerResultFullName = "Kaleido.Processor.Execution.ProcessStepHandlerResult";

    private static readonly string[] HandlerInterfaces =
    [
        "Kaleido.Processor.Execution.IProcessStepHandler<TProcessStep>",
        "Kaleido.Processor.Execution.IProcessStepHandler<TProcessStep, TProcessStepResult>"
    ];

    // The relationships that make a step run after others; AvailableUntil only closes a step.
    private static readonly string[] AfterOtherSteps =
    [
        "Kaleido.Processor.DependsOnAttribute<TStep>",
        "Kaleido.Processor.AvailableAfterAttribute<TStep>"
    ];

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.EntryStepCompletesProcess,
            "An entry step's handler completes the process",
            "'{0}' completes the process, but '{1}' is an entry step (no [DependsOn] or [AvailableAfter]): it could end a process in its first step. Declare the steps it runs after.",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinks.For(DiagnosticIds.EntryStepCompletesProcess),
            description: "Complete() ends a process whatever its rules still offer; a step that can start a process should not do that.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var method = ((IInvocationOperation)context.Operation).TargetMethod;

        if (method.Name != "Complete" ||
            !method.IsStatic ||
            method.ContainingType.OriginalDefinition.ToDisplayString() != HandlerResultFullName ||
            HandledStep(context.ContainingSymbol.ContainingType) is not { } step ||
            step.GetAttributes().Any(x => x.AttributeClass is { } attribute &&
                                          Array.IndexOf(AfterOtherSteps, attribute.OriginalDefinition.ToDisplayString()) >= 0))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, context.Operation.Syntax.GetLocation(), context.ContainingSymbol.ContainingType.Name, step.Name));
    }

    // The step the containing type handles (its IProcessStepHandler<TStep...>), or null outside a handler.
    private static INamedTypeSymbol? HandledStep(INamedTypeSymbol? handler) =>
        handler?.AllInterfaces
            .FirstOrDefault(x => Array.IndexOf(HandlerInterfaces, x.OriginalDefinition.ToDisplayString()) >= 0)?
            .TypeArguments[0] as INamedTypeSymbol;
}
