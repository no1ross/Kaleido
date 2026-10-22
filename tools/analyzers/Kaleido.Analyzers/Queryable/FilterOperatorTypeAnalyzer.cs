using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Queryable;

/// <summary>
/// KAL2020 — a [Filterable] operator does not apply to the property's type: a comparison
/// (GreaterThan, Between, ...) on a type that cannot be compared, a string operator (Contains,
/// StartsWith, ...) on a non-string, IsTrue/IsFalse on a non-bool, IsNull/IsNotNull on a type that
/// cannot be null, or Equals/NotEquals on a struct without an equality operator. Startup fails with
/// qry_invalid_registration; without the check the registry would advertise an operator that fails
/// every request using it.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FilterOperatorTypeAnalyzer : DiagnosticAnalyzer
{
    private const string FilterableAttributeFullName = "Kaleido.Queryable.FilterableAttribute";

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.FilterOperatorTypeMismatch,
            "A filter operator must apply to the property's type",
            "Filter operator '{0}' does not apply to property '{1}' of type '{2}': {3}",
            "Kaleido.Usage",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinks.For(DiagnosticIds.FilterOperatorTypeMismatch),
            description: "Kaleido checks [Filterable] operators against the property's type at startup (qry_invalid_registration). A comparison needs a number, date, time or other type with comparison operators; string operators need a string; IsTrue/IsFalse need a bool; IsNull/IsNotNull need a nullable type.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.Property);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var property = (IPropertySymbol)context.Symbol;

        var attribute = QueryableSymbols.FindAttribute(property, FilterableAttributeFullName);

        if (attribute is null ||
            attribute.ConstructorArguments.Length != 1 ||
            attribute.ConstructorArguments[0] is not { Kind: TypedConstantKind.Array, Type: IArrayTypeSymbol { ElementType: INamedTypeSymbol operatorType } } operators ||
            property.Type is ITypeParameterSymbol)
        {
            return;
        }

        var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation() ?? Location.None;

        foreach (var value in operators.Values)
        {
            var name = operatorType.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(x => x.HasConstantValue && Equals(x.ConstantValue, value.Value))?.Name;

            if (name is not null && Misfit(name, property.Type) is { } reason)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, location, name, property.Name, property.Type.ToDisplayString(), reason));
            }
        }
    }

    private static string? Misfit(string @operator, ITypeSymbol type)
    {
        var isNullableValue = type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        var underlying = isNullableValue ? ((INamedTypeSymbol)type).TypeArguments[0] : type;

        return @operator switch
        {
            "Equals" or "NotEquals" when !HasEquality(underlying) =>
                "the type has no equality operator",
            "GreaterThan" or "GreaterThanOrEqual" or "LessThan" or "LessThanOrEqual" or "Between" or "NotBetween" when !IsComparable(underlying) =>
                "the type cannot be compared (a number, date, time or other type with comparison operators)",
            "Contains" or "NotContains" or "StartsWith" or "EndsWith" when type.SpecialType != SpecialType.System_String =>
                "it applies to string fields only",
            "IsTrue" or "IsFalse" when underlying.SpecialType != SpecialType.System_Boolean =>
                "it applies to bool fields only",
            "IsNull" or "IsNotNull" when type.IsValueType && !isNullableValue =>
                "the type cannot be null",
            _ => null,
        };
    }

    // Reference types compare by reference; primitives and enums have built-in equality.
    private static bool HasEquality(ITypeSymbol type) =>
        !type.IsValueType ||
        type.TypeKind == TypeKind.Enum ||
        type.SpecialType != SpecialType.None ||
        type.GetMembers("op_Equality").Length > 0;

    // Built-in comparison for the numeric primitives; otherwise a declared comparison operator
    // (DateTime, DateTimeOffset, DateOnly, TimeSpan, Guid, ...). Not string, bool, char or enums.
    private static bool IsComparable(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16
            or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single
            or SpecialType.System_Double or SpecialType.System_Decimal ||
        (type.TypeKind != TypeKind.Enum && type.GetMembers("op_GreaterThan").Length > 0);
}
