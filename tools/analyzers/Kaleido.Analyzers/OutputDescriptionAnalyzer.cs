using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Kaleido.Analyzers.Process;
using Kaleido.Analyzers.Queryable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// KAL2021: what a step returns (<c>TResult</c> of <c>IProcessStepHandler&lt;TStep, TResult&gt;</c>) and what
/// a view returns (<c>TView</c> of <c>IQueryViewSource*</c>) is read by agents and UIs; the registry
/// publishes each property's description, and the MCP adapter hands it to the agent with the data.
/// A source's output is its query context, already covered by KAL2015.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OutputDescriptionAnalyzer : DiagnosticAnalyzer
{
    private const string HandlerInterfaceDefinition = "Kaleido.Processor.IProcessStepHandler<TStep, TResult>";

    private static readonly string[] ViewInterfaceDefinitions =
    [
        "Kaleido.Queryable.IQueryViewSource<TSource, TQueryContext, TView, TViewParameters>",
        "Kaleido.Queryable.IQueryViewSourceAsync<TSource, TQueryContext, TView, TViewParameters>"
    ];

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.OutputPropertyDescription,
            "Describe step results and view outputs",
            "Property '{0}' of '{1}' has no description. Add [Description(\"…\")] so AI agents and UIs know what the returned value means.",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinks.For(DiagnosticIds.OutputPropertyDescription),
            description: "Step result and view output descriptions are published in the registry, and the MCP adapter returns them with the data so an agent knows what each field means.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // An output type used by several handlers or views is reported once.
            var reported = new ConcurrentDictionary<ITypeSymbol, bool>(SymbolEqualityComparer.Default);

            start.RegisterSymbolAction(symbol => Analyze(symbol, reported), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, ConcurrentDictionary<ITypeSymbol, bool> reported)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class || type.IsAbstract)
        {
            return;
        }

        // TryAdd claims an output type, so one used by several handlers or views is reported once.
        foreach (var output in Outputs(type).OfType<INamedTypeSymbol>().Where(IsDescribedHere).Where(x => reported.TryAdd(x, true)))
        {
            Report(context, output);
        }
    }

    // A class or struct declared in this project that no other rule covers (query contexts: KAL2015).
    private static bool IsDescribedHere(INamedTypeSymbol output) =>
        output.TypeKind is TypeKind.Class or TypeKind.Struct &&
        output.Locations.Any(location => location.IsInSource) &&
        !QueryableSymbols.IsQueryContext(output) &&
        !ProcessStepSymbols.ImplementsProcessStep(output);

    // The result types of the step handlers and the output records of the views the type implements.
    private static IEnumerable<ITypeSymbol> Outputs(INamedTypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            var definition = iface.OriginalDefinition.ToDisplayString();

            if (definition == HandlerInterfaceDefinition)
            {
                yield return iface.TypeArguments[1];
            }
            else if (ViewInterfaceDefinitions.Contains(definition))
            {
                yield return iface.TypeArguments[2];
            }
        }
    }

    private static void Report(SymbolAnalysisContext context, INamedTypeSymbol output)
    {
        foreach (var member in output.GetMembers())
        {
            if (member is not IPropertySymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } property ||
                property.Name == "EqualityContract" ||
                property.IsImplicitlyDeclared ||
                AttributeHelper.HasDescription(property))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(Rule, property.Locations.FirstOrDefault() ?? Location.None, property.Name, output.Name));
        }
    }
}
