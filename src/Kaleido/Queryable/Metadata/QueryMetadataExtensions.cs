using System.ComponentModel;
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
            FilterOperators = filterable?.Operators ?? [],
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
            attribute.Version,
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
