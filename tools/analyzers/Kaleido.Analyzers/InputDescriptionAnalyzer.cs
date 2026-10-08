using System.Collections.Immutable;
using Kaleido.Analyzers.Process;
using Kaleido.Analyzers.Queryable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// KAL2017 — a process step input property without a description.
/// KAL2018 — a query context or query parameters property without a description.
/// Descriptions are what UIs and AI agents (MCP tool schemas) read to know what an input is.
/// Only a description is asked for: [Description("…")] or [Display(Description = "…")];
/// Display Name and Prompt stay optional hints. Warnings, not errors.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InputDescriptionAnalyzer : DiagnosticAnalyzer
{
    private const string DescriptionAttributeFullName = "System.ComponentModel.DescriptionAttribute";
    private const string DisplayAttributeFullName = "System.ComponentModel.DataAnnotations.DisplayAttribute";
    private const string InformationStepInterfaceFullName = "Kaleido.Processor.IInformationStep";
    private const string QueryParametersInterfaceFullName = "Kaleido.Queryable.IQueryParameters";

    private static readonly DiagnosticDescriptor StepRule =
        new(
            DiagnosticIds.ProcessStepPropertyDescription,
            "Describe process step inputs",
            "Property '{0}' of process step '{1}' has no description. Add [Description(\"…\")] or [Display(Description = \"…\")] so UIs and AI agents know what to ask for.",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Step input descriptions are published in the registry and are what clients and AI agents rely on to collect the right information.");

    private static readonly DiagnosticDescriptor QueryRule =
        new(
            DiagnosticIds.QueryPropertyDescription,
            "Describe query context and parameter properties",
            "Property '{0}' of '{1}' has no description. Add [Description(\"…\")] so UIs and AI agents know what it means.",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Query context and parameter descriptions are published in the registry and become field descriptions for clients and AI agents.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(StepRule, QueryRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class || type.IsAbstract)
        {
            return;
        }

        var rule =
            ProcessStepSymbols.ImplementsProcessStep(type) &&
            !ProcessStepSymbols.ImplementsProcessStep(type, InformationStepInterfaceFullName)
                ? StepRule
                : QueryableSymbols.IsQueryContext(type) ||
                  ProcessStepSymbols.ImplementsProcessStep(type, QueryParametersInterfaceFullName)
                    ? QueryRule
                    : null;

        if (rule is null)
        {
            return;
        }

        foreach (var member in type.GetMembers())
        {
            if (member is not IPropertySymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } property ||
                property.Name == "EqualityContract" ||
                HasDescription(property))
            {
                continue;
            }

            var location = property.Locations.Length > 0
                ? property.Locations[0]
                : Location.None;

            context.ReportDiagnostic(
                Diagnostic.Create(rule, location, property.Name, type.Name));
        }
    }

    private static bool HasDescription(IPropertySymbol property)
    {
        foreach (var attribute in property.GetAttributes())
        {
            switch (attribute.AttributeClass?.ToDisplayString())
            {
                case DescriptionAttributeFullName
                    when attribute.ConstructorArguments.Length == 1 &&
                         attribute.ConstructorArguments[0].Value is string text &&
                         !string.IsNullOrWhiteSpace(text):
                    return true;

                case DisplayAttributeFullName:
                    foreach (var argument in attribute.NamedArguments)
                    {
                        if (argument.Key == "Description" &&
                            argument.Value.Value is string description &&
                            !string.IsNullOrWhiteSpace(description))
                        {
                            return true;
                        }
                    }

                    break;
            }
        }

        return false;
    }
}
