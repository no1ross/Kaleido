using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using Kaleido.Registry;

namespace Kaleido.Queryable.Metadata;

internal static class QueryMetadataExtensions
{
    /// <summary>
    /// Derives field metadata for a query-context property from its CLR type and
    /// <see cref="FilterableAttribute"/> / <see cref="SearchableAttribute"/> /
    /// <see cref="SortableAttribute"/> decorations.
    /// </summary>
    internal static FieldMetadata ToFieldMetadata(
        this PropertyInfo property,
        ITypeDescriber typeDescriber)
    {
        var filterable =
            property.GetCustomAttribute<FilterableAttribute>();

        var searchable =
            property.GetCustomAttribute<SearchableAttribute>();

        var sortable =
            property.GetCustomAttribute<SortableAttribute>();

        var description =
            property.GetCustomAttribute<DescriptionAttribute>();

        return new FieldMetadata
        {
            Name = property.Name,
            Description = description?.Description,
            FieldType = property.PropertyType,
            DataType = typeDescriber.GetDescriptor(property),
            IsFilterable = filterable is not null,
            FilterOperators = filterable is null ? [] : property.ValidateFilterOperators(filterable.Operators),
            IsSearchable = searchable is not null,
            SearchPriority = searchable?.Priority,
            MatchMode = searchable?.MatchMode,
            IsSortable = sortable is not null,
            Classifications = property.GetDataClassificationNames(),
        };
    }

    /// <summary>
    /// Derives parameter metadata for each public instance property of a view-parameters
    /// type. <see cref="EmptyQueryViewParameters"/> yields an empty list.
    /// </summary>
    internal static IReadOnlyList<QueryParameterMetadata> ToParameterMetadata(
        this Type parametersType,
        ITypeDescriber typeDescriber,
        IConstraintMapper constraintMapper)
    {
        if (parametersType == typeof(EmptyQueryViewParameters))
        {
            return [];
        }

        return parametersType
            .GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance)
            .Select(property =>
                new QueryParameterMetadata
                {
                    Name = property.Name,
                    Type = property.PropertyType,
                    DataType = typeDescriber.GetDescriptor(property),
                    Constraints = constraintMapper.Map(property),
                    Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description,
                    Classifications = property.GetDataClassificationNames(),
                })
            .ToArray();
    }

    /// <summary>
    /// Derives output-field metadata for each public instance property of a view type.
    /// </summary>
    internal static IReadOnlyList<QueryOutputFieldMetadata> ToOutputFieldMetadata(
        this Type viewType,
        ITypeDescriber typeDescriber) =>
        viewType
            .GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance)
            .Select(property =>
                new QueryOutputFieldMetadata
                {
                    Name = property.Name,
                    Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description,
                    Type = property.PropertyType,
                    DataType = typeDescriber.GetDescriptor(property),
                    Classifications = property.GetDataClassificationNames(),
                })
            .ToArray();

    /// <summary>
    /// Builds <see cref="QueryViewMetadata"/> from a <see cref="QueryViewAttribute"/>
    /// and the discovered interface types of the query view.
    /// </summary>
    internal static QueryViewMetadata ToViewMetadata(
        this QueryViewAttribute attribute,
        Type queryViewType,
        Type contextType,
        Type parametersType,
        Type viewType,
        ITypeDescriber typeDescriber,
        IConstraintMapper constraintMapper) =>
        new(
            queryViewType.Name,
            attribute.DisplayName,
            attribute.Description,
            queryViewType.ToViewPageable(contextType, attribute),
            parametersType.ToParameterMetadata(typeDescriber, constraintMapper),
            viewType.ToOutputFieldMetadata(typeDescriber),
            AuthorizationMetadata.ForType(queryViewType));

    /// <summary>
    /// Builds pageable metadata for a query view from its <see cref="PageableAttribute"/>,
    /// validating the required <c>DefaultSortField</c> against the context type.
    /// </summary>
    internal static PageableMetadata? ToViewPageable(
        this Type queryViewType,
        Type contextType,
        QueryViewAttribute attribute)
    {
        var pageable =
            queryViewType.GetCustomAttribute<PageableAttribute>();

        return pageable is null
            ? null
            : new PageableMetadata(
                pageable.DefaultSize,
                pageable.MaxSize,
                ValidateDefaultSort("Query view", queryViewType, contextType, attribute.DefaultSortField));
    }

    /// <summary>
    /// Builds pageable metadata for a query source from its <see cref="PageableAttribute"/>. A local
    /// source must declare a sortable <c>DefaultSortField</c>, so its pages are stable; a delegated
    /// source pages in its downstream, which owns the order.
    /// </summary>
    internal static PageableMetadata? ToSourcePageable(
        this Type sourceType,
        Type contextType,
        QuerySourceAttribute attribute,
        QuerySourceKind kind)
    {
        var pageable =
            sourceType.GetCustomAttribute<PageableAttribute>();

        return pageable is null
            ? null
            : new PageableMetadata(
                pageable.DefaultSize,
                pageable.MaxSize,
                kind == QuerySourceKind.Local
                    ? ValidateDefaultSort("Query source", sourceType, contextType, attribute.DefaultSortField)
                    : null);
    }

    // Every declared filter operator must apply to the field's type, the way the query engine
    // (CompiledQueryApplier) builds it; otherwise the registry would advertise an operator that
    // fails every request that uses it. Analyzer KAL2020 reports the same mistakes at compile time.
    private static IReadOnlyList<FilterOperator> ValidateFilterOperators(
        this PropertyInfo property,
        IReadOnlyList<FilterOperator> operators)
    {
        var type = property.PropertyType;

        foreach (var @operator in operators)
        {
            var reason = @operator switch
            {
                FilterOperator.Equals or FilterOperator.NotEquals when !Builds(Expression.Equal, type) =>
                    "the type has no equality operator",
                FilterOperator.GreaterThan or FilterOperator.GreaterThanOrEqual or FilterOperator.LessThan or FilterOperator.LessThanOrEqual
                    or FilterOperator.Between or FilterOperator.NotBetween when !Builds(Expression.GreaterThan, type) =>
                    "the type cannot be compared (a number, date, time or other type with comparison operators)",
                FilterOperator.Contains or FilterOperator.NotContains or FilterOperator.StartsWith or FilterOperator.EndsWith
                    when type != typeof(string) =>
                    "it applies to string fields only",
                FilterOperator.IsTrue or FilterOperator.IsFalse when (Nullable.GetUnderlyingType(type) ?? type) != typeof(bool) =>
                    "it applies to bool fields only",
                FilterOperator.IsNull or FilterOperator.IsNotNull when type.IsValueType && Nullable.GetUnderlyingType(type) is null =>
                    "the type cannot be null",
                _ => null,
            };

            if (reason is not null)
            {
                throw new KaleidoConfigurationException(
                    QueryableErrorCodes.InvalidRegistration,
                    $"Query context '{property.DeclaringType?.Name}' field '{property.Name}' ({type.Name}) declares filter operator '{@operator}', which does not apply: {reason}.");
            }
        }

        return operators;
    }

    private static bool Builds(Func<Expression, Expression, BinaryExpression> build, Type type)
    {
        try
        {
            build(Expression.Default(type), Expression.Default(type));
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    // The default sort field, by its declared property name, after checking it exists and is sortable.
    private static string ValidateDefaultSort(
        string capability,
        Type capabilityType,
        Type contextType,
        string? defaultSortField)
    {
        if (string.IsNullOrWhiteSpace(defaultSortField))
        {
            throw new KaleidoConfigurationException(
                QueryableErrorCodes.InvalidRegistration,
                $"{capability} '{capabilityType.Name}' is pageable and must define a DefaultSortField, so its pages are stable.");
        }

        var property =
            contextType.GetProperty(
                defaultSortField,
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.IgnoreCase)
            ?? throw new KaleidoConfigurationException(
                QueryableErrorCodes.InvalidRegistration,
                $"{capability} '{capabilityType.Name}' specifies DefaultSortField '{defaultSortField}' which does not exist on query context '{contextType.Name}'.");

        if (!Attribute.IsDefined(property, typeof(SortableAttribute)))
        {
            throw new KaleidoConfigurationException(
                QueryableErrorCodes.InvalidRegistration,
                $"{capability} '{capabilityType.Name}' specifies DefaultSortField '{defaultSortField}' but the field is not marked as sortable.");
        }

        return property.Name;
    }
}
