using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Kaleido.Analyzers.Process;

/// <summary>
/// KAL2015 — an IInformationStep declares a property other than InformationRequestId and Items.
/// KAL2016 — Success&lt;TNext&gt;() names an information step, which needs a request to answer.
/// Both fail at runtime (pro_invalid_registration at startup, pro_information_request_missing
/// when the step completes), so they are reported at compile time.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InformationStepAnalyzer : DiagnosticAnalyzer
{
    private const string InformationStepInterfaceFullName = "Kaleido.Processor.IInformationStep";

    private static readonly string[] HandlerResultTypes =
    [
        "Kaleido.Processor.Execution.ProcessStepHandlerResult",
        "Kaleido.Processor.Execution.ProcessStepHandlerResult<TProcessStepResult>"
    ];

    private static readonly DiagnosticDescriptor ShapeRule =
        new(
            DiagnosticIds.InformationStepShape,
            "An information step declares only InformationRequestId and Items",
            "Information step '{0}' declares '{1}'. Its payload is the answers to an information request; put known inputs on an ordinary step instead.",
            "Kaleido.Usage",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Startup fails with pro_invalid_registration when an IInformationStep declares other properties: the same data must never live in two places.");

    private static readonly DiagnosticDescriptor SuccessRule =
        new(
            DiagnosticIds.InformationStepRequiredWithoutRequest,
            "Require an information step with RequireInformation",
            "'{0}' is an information step; require it with RequireInformation<{0}>(request) so there are questions to answer",
            "Kaleido.Usage",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Requiring an information step without an information request fails the step with pro_information_request_missing.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(ShapeRule, SuccessRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class || !IsInformationStep(type))
        {
            return;
        }

        foreach (var property in DeclaredProperties(type))
        {
            if (property.Name is "InformationRequestId" or "Items")
            {
                continue;
            }

            var location = property.Locations.Length > 0
                ? property.Locations[0]
                : Location.None;

            context.ReportDiagnostic(
                Diagnostic.Create(ShapeRule, location, type.Name, property.Name));
        }
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var method = ((IInvocationOperation)context.Operation).TargetMethod;

        if (method.Name != "Success" ||
            method.TypeArguments.Length != 1 ||
            Array.IndexOf(HandlerResultTypes, method.ContainingType.OriginalDefinition.ToDisplayString()) < 0 ||
            method.TypeArguments[0] is not INamedTypeSymbol next ||
            !IsInformationStep(next))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(SuccessRule, context.Operation.Syntax.GetLocation(), next.Name));
    }

    private static bool IsInformationStep(INamedTypeSymbol type) =>
        ProcessStepSymbols.ImplementsProcessStep(type, InformationStepInterfaceFullName);

    // Public instance properties of the step and its base types (positional record members
    // included); the compiler's record EqualityContract is excluded.
    private static IEnumerable<IPropertySymbol> DeclaredProperties(INamedTypeSymbol type)
    {
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var property in current.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property => !property.IsStatic &&
                                   property.DeclaredAccessibility == Accessibility.Public &&
                                   property.Name != "EqualityContract"))
            {
                yield return property;
            }
        }
    }
}
