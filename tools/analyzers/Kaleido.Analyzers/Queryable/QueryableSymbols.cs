using Microsoft.CodeAnalysis;

namespace Kaleido.Analyzers.Queryable;

internal static class QueryableSymbols
{
    public const string SourceAttributeFullName = "Kaleido.Queryable.QuerySourceAttribute";

    public const string ViewAttributeFullName = "Kaleido.Queryable.QueryViewAttribute";

    public const string QueryContextInterfaceFullName = "Kaleido.Queryable.IQueryContext";

    // Open generic interface names as rendered by ToDisplayString() of OriginalDefinition.
    private static readonly string[] SourceInterfaceDefinitions =
    [
        "Kaleido.Queryable.IQuerySource<TQueryContext>",
        "Kaleido.Queryable.IQuerySourceAsync<TQueryContext>",
        "Kaleido.Queryable.IDelegatedQuerySource<TQueryContext, TResult, TParameters>"
    ];

    private static readonly string[] ViewInterfaceDefinitions =
    [
        "Kaleido.Queryable.IQueryViewSource<TSource, TQueryContext, TView, TViewParameters>",
        "Kaleido.Queryable.IQueryViewSourceAsync<TSource, TQueryContext, TView, TViewParameters>"
    ];

    // Property attributes that only have meaning on an IQueryContext record.
    public static readonly string[] QueryRuleAttributeFullNames =
    [
        "Kaleido.Queryable.FilterableAttribute",
        "Kaleido.Queryable.SearchableAttribute",
        "Kaleido.Queryable.SortableAttribute"
    ];

    public static bool IsQuerySource(INamedTypeSymbol type) =>
        ImplementsAny(type, SourceInterfaceDefinitions);

    public static bool IsQueryView(INamedTypeSymbol type) =>
        ImplementsAny(type, ViewInterfaceDefinitions);

    public static bool IsQueryContext(INamedTypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.ToDisplayString() == QueryContextInterfaceFullName)
            {
                return true;
            }
        }

        return false;
    }

    public static AttributeData? FindAttribute(
        ISymbol symbol,
        string attributeFullName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == attributeFullName)
            {
                return attribute;
            }
        }

        return null;
    }

    private static bool ImplementsAny(
        INamedTypeSymbol type,
        string[] openDefinitions)
    {
        foreach (var iface in type.AllInterfaces)
        {
            var definition = iface.OriginalDefinition.ToDisplayString();

            foreach (var candidate in openDefinitions)
            {
                if (definition == candidate)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
