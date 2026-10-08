using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Process;

/// <summary>
/// KAL2010 — [ProcessStep] applied to a type that does not implement IProcessStep.
/// KAL2011 — a concrete IProcessStep type without the required [ProcessStep] attribute.
/// IProcessStep is the step's identity; [ProcessStep] only describes it. Both mismatches
/// fail startup registration, so they are reported at compile time.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProcessStepIdentityAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor AttributeWithoutInterfaceRule =
        new(
            DiagnosticIds.ProcessStepAttributeWithoutInterface,
            "[ProcessStep] type must implement IProcessStep",
            "Type '{0}' has [ProcessStep] but does not implement IProcessStep. A process step is identified by IProcessStep; the attribute only describes it.",
            "Kaleido.Usage",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Startup fails with pro_invalid_registration when [ProcessStep] is applied to a type that does not implement IProcessStep.");

    private static readonly DiagnosticDescriptor InterfaceWithoutAttributeRule =
        new(
            DiagnosticIds.ProcessStepMissingAttribute,
            "IProcessStep type must have [ProcessStep]",
            "Process step '{0}' implements IProcessStep but is missing the required [ProcessStep] attribute (Version, DisplayName, Description)",
            "Kaleido.Usage",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Startup fails with pro_missing_attribute when a concrete IProcessStep type has no [ProcessStep] attribute.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(AttributeWithoutInterfaceRule, InterfaceWithoutAttributeRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class)
        {
            return;
        }

        var hasAttribute = ProcessStepSymbols.FindProcessStepAttribute(type) is not null;
        var implementsInterface = ProcessStepSymbols.ImplementsProcessStep(type);

        if (hasAttribute && !implementsInterface)
        {
            Report(context, AttributeWithoutInterfaceRule, type);
        }
        else if (implementsInterface && !hasAttribute && !type.IsAbstract)
        {
            Report(context, InterfaceWithoutAttributeRule, type);
        }
    }

    private static void Report(
        SymbolAnalysisContext context,
        DiagnosticDescriptor rule,
        INamedTypeSymbol type)
    {
        var location = type.Locations.Length > 0
            ? type.Locations[0]
            : Location.None;

        context.ReportDiagnostic(
            Diagnostic.Create(rule, location, type.Name));
    }
}
