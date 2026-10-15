using System.Collections.Immutable;
using Kaleido.Analyzers.Process;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// KAL2021 — a process step input marked [Required] has a non-nullable value type and is not a
/// C# <c>required</c> member.
/// KAL2022 — the same for a query parameter.
/// The [Required] attribute can only fail on a value that can be missing: an omitted Guid, DateOnly,
/// enum or number becomes its default and passes, so the input is silently "provided". The
/// <c>required</c> keyword makes the caller supply it (the compiler for in-process callers,
/// System.Text.Json for a request body); a nullable type lets [Required] see it missing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RequiredValueTypeAnalyzer : DiagnosticAnalyzer
{
    private const string RequiredAttributeFullName = "System.ComponentModel.DataAnnotations.RequiredAttribute";
    private const string QueryParametersInterfaceFullName = "Kaleido.Queryable.IQueryParameters";

    private static readonly DiagnosticDescriptor StepRule =
        new(
            DiagnosticIds.ProcessStepRequiredValueType,
            "Declare required step inputs with the required keyword",
            "Property '{0}' of process step '{1}' is [Required] but '{2}' cannot be missing: an omitted value becomes its default and passes. Declare it 'public required {2} {0}' (or as '{2}?').",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "The [Required] attribute fails only for a missing (null) value. For a value type, the required keyword makes callers supply it: the compiler for in-process callers, System.Text.Json for a request body.");

    private static readonly DiagnosticDescriptor QueryRule =
        new(
            DiagnosticIds.QueryParameterRequiredValueType,
            "Declare required query parameters with the required keyword",
            "Property '{0}' of query parameters '{1}' is [Required] but '{2}' cannot be missing: an omitted value becomes its default and passes. Declare it 'public required {2} {0}' (or as '{2}?').",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "The [Required] attribute fails only for a missing (null) value. For a value type, the required keyword makes callers supply it: the compiler for in-process callers, System.Text.Json for a request body.");

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

        if (type.IsAbstract || type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return;
        }

        var rule =
            ProcessStepSymbols.ImplementsProcessStep(type)
                ? StepRule
                : ProcessStepSymbols.ImplementsProcessStep(type, QueryParametersInterfaceFullName)
                    ? QueryRule
                    : null;

        if (rule is null)
        {
            return;
        }

        foreach (var member in type.GetMembers())
        {
            if (member is not IPropertySymbol { IsStatic: false, IsRequired: false, DeclaredAccessibility: Accessibility.Public } property ||
                !IsNonNullableValueType(property.Type) ||
                !IsRequired(property))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    rule,
                    property.Locations.Length > 0 ? property.Locations[0] : Location.None,
                    property.Name,
                    type.Name,
                    property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    private static bool IsNonNullableValueType(ITypeSymbol type) =>
        type.IsValueType &&
        type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;

    private static bool IsRequired(IPropertySymbol property)
    {
        foreach (var attribute in property.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == RequiredAttributeFullName)
            {
                return true;
            }
        }

        return false;
    }
}
