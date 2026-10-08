using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Queryable;

/// <summary>
/// Queryable identity rules. The interfaces are the identity; the attributes only describe.
/// <list type="bullet">
/// <item><description>KAL2012 — [QuerySource] or [QueryView] on a type that does not implement the matching interface (discovery never sees it).</description></item>
/// <item><description>KAL2013 — a concrete query source or view without its required attribute (startup fails).</description></item>
/// <item><description>KAL2014 — [Filterable], [Searchable] or [Sortable] on a property of a type that is not an IQueryContext (the rule is ignored).</description></item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QueryableIdentityAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor AttributeWithoutInterfaceRule =
        new(
            DiagnosticIds.QueryableAttributeWithoutInterface,
            "Queryable attribute requires the matching interface",
            "Type '{0}' has [{1}] but does not implement {2}. Queryable capabilities are discovered by interface; the attribute only describes them.",
            "Kaleido.Usage",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Discovery is interface-only, so a [QuerySource] or [QueryView] on a type without the matching interface is never registered.");

    private static readonly DiagnosticDescriptor InterfaceWithoutAttributeRule =
        new(
            DiagnosticIds.QueryableMissingAttribute,
            "Queryable capability must have its attribute",
            "{0} '{1}' is missing the required [{2}] attribute (Version, DisplayName, Description)",
            "Kaleido.Usage",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Startup fails with qry_missing_attribute when a concrete query source or view has no [QuerySource] or [QueryView] attribute.");

    private static readonly DiagnosticDescriptor QueryRuleOutsideContextRule =
        new(
            DiagnosticIds.QueryRuleAttributeOutsideQueryContext,
            "Query rule attributes belong on IQueryContext records",
            "[{0}] on '{1}.{2}' has no effect: '{1}' does not implement IQueryContext",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "[Filterable], [Searchable] and [Sortable] describe how a query source can be queried and are only read from IQueryContext records.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            AttributeWithoutInterfaceRule,
            InterfaceWithoutAttributeRule,
            QueryRuleOutsideContextRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
        context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class)
        {
            return;
        }

        var isSource = QueryableSymbols.IsQuerySource(type);
        var isView = QueryableSymbols.IsQueryView(type);
        var hasSourceAttribute = QueryableSymbols.FindAttribute(type, QueryableSymbols.SourceAttributeFullName) is not null;
        var hasViewAttribute = QueryableSymbols.FindAttribute(type, QueryableSymbols.ViewAttributeFullName) is not null;

        if (hasSourceAttribute && !isSource)
        {
            Report(context, AttributeWithoutInterfaceRule, type, type.Name, "QuerySource", "IQuerySource<T>, IQuerySourceAsync<T> or IDelegatedQuerySource<…>");
        }

        if (hasViewAttribute && !isView)
        {
            Report(context, AttributeWithoutInterfaceRule, type, type.Name, "QueryView", "IQueryViewSource<…> or IQueryViewSourceAsync<…>");
        }

        if (type.IsAbstract)
        {
            return;
        }

        if (isSource && !hasSourceAttribute)
        {
            Report(context, InterfaceWithoutAttributeRule, type, "Query source", type.Name, "QuerySource");
        }

        if (isView && !hasViewAttribute)
        {
            Report(context, InterfaceWithoutAttributeRule, type, "Query view", type.Name, "QueryView");
        }
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context)
    {
        var property = (IPropertySymbol)context.Symbol;

        if (property.ContainingType is not { } containingType ||
            QueryableSymbols.IsQueryContext(containingType))
        {
            return;
        }

        foreach (var attributeName in QueryableSymbols.QueryRuleAttributeFullNames)
        {
            if (QueryableSymbols.FindAttribute(property, attributeName) is not { } attribute)
            {
                continue;
            }

            var location =
                attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                ?? (property.Locations.Length > 0 ? property.Locations[0] : Location.None);

            context.ReportDiagnostic(
                Diagnostic.Create(
                    QueryRuleOutsideContextRule,
                    location,
                    (attribute.AttributeClass?.Name ?? attributeName).Replace("Attribute", string.Empty),
                    containingType.Name,
                    property.Name));
        }
    }

    private static void Report(
        SymbolAnalysisContext context,
        DiagnosticDescriptor rule,
        INamedTypeSymbol type,
        params object[] arguments)
    {
        var location = type.Locations.Length > 0
            ? type.Locations[0]
            : Location.None;

        context.ReportDiagnostic(
            Diagnostic.Create(rule, location, arguments));
    }
}
