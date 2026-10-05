using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Kaleido.Analyzers.Source.Design;

/// <summary>
/// KAL0020 — <c>.WithName(...)</c> must take its name from an <c>*EndpointNames</c>
/// constant or factory method, never an ad-hoc string. Endpoint names are link
/// targets (<c>LinkGenerator</c>, OpenAPI operation ids); an unenforced literal let
/// the <c>KaleidoProcessStepREgistry</c> typo ship (HP-015, #62).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EndpointNameConstantAnalyzer : DiagnosticAnalyzer
{
    private const string EndpointNamesSuffix = "EndpointNames";

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.EndpointNameConstant,
            "Endpoint names must come from an *EndpointNames type",
            "Pass a member of an *EndpointNames type to WithName instead of '{0}'",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

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
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (method.Name != "WithName" ||
            !(method.ContainingNamespace?.ToDisplayString().StartsWith("Microsoft.AspNetCore", System.StringComparison.Ordinal) ?? false))
        {
            return;
        }

        var nameArgument =
            invocation.Arguments.FirstOrDefault(a => a.Parameter?.Type.SpecialType == SpecialType.System_String);

        if (nameArgument is null || IsFromEndpointNames(nameArgument.Value))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, nameArgument.Value.Syntax.GetLocation(), nameArgument.Value.Syntax.ToString()));
    }

    private static bool IsFromEndpointNames(IOperation value)
    {
        while (value is IConversionOperation conversion)
        {
            value = conversion.Operand;
        }

        return value switch
        {
            IFieldReferenceOperation field => IsEndpointNamesType(field.Field.ContainingType),
            IInvocationOperation call => IsEndpointNamesType(call.TargetMethod.ContainingType),
            _ => false
        };
    }

    private static bool IsEndpointNamesType(INamedTypeSymbol? type) =>
        type?.Name.EndsWith(EndpointNamesSuffix, System.StringComparison.Ordinal) == true;
}
