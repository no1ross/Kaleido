using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Process;

/// <summary>
/// KAL2008 — every IProcessStep type must have an IProcessStepHandler&lt;TStep&gt; (or
/// IProcessStepHandler&lt;TStep, TResult&gt;) implementation in the same compilation.
/// A step without a handler cannot be executed by the framework.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProcessStepHandlerAnalyzer : DiagnosticAnalyzer
{
    private const string HandlerInterfaceName = "IProcessStepHandler";

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.ProcessStepMissingHandler,
            "IProcessStep type has no handler in this compilation",
            "Process step '{0}' has no IProcessStepHandler<{0}> in this compilation. Register a handler or this step cannot be executed.",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Every IProcessStep type must have a corresponding IProcessStepHandler<TStep> in the same compilation for the framework to execute it.",
            customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var stepTypes = new ConcurrentBag<INamedTypeSymbol>();
        var handledTypes = new ConcurrentBag<INamedTypeSymbol>();

        context.RegisterSymbolAction(
            symbolContext =>
            {
                var type = (INamedTypeSymbol)symbolContext.Symbol;

                // Collect concrete IProcessStep types
                if (type.TypeKind == TypeKind.Class && !type.IsAbstract &&
                    ProcessStepSymbols.ImplementsProcessStep(type))
                {
                    stepTypes.Add(type);
                }

                // Collect types handled by IProcessStepHandler<TStep> or <TStep, TResult>
                foreach (var iface in type.AllInterfaces)
                {
                    if (iface.Name == HandlerInterfaceName &&
                        iface.TypeArguments.Length >= 1 &&
                        iface.TypeArguments[0] is INamedTypeSymbol stepArg)
                    {
                        handledTypes.Add(stepArg);
                    }
                }
            },
            SymbolKind.NamedType);

        context.RegisterCompilationEndAction(endContext =>
        {
            foreach (var stepType in stepTypes)
            {
                var hasHandler = false;
                foreach (var handled in handledTypes)
                {
                    if (SymbolEqualityComparer.Default.Equals(stepType, handled))
                    {
                        hasHandler = true;
                        break;
                    }
                }

                if (!hasHandler)
                {
                    var location = stepType.Locations.Length > 0
                        ? stepType.Locations[0]
                        : Location.None;

                    endContext.ReportDiagnostic(
                        Diagnostic.Create(Rule, location, stepType.Name));
                }
            }
        });
    }
}
