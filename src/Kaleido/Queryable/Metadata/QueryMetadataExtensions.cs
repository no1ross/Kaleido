using System.ComponentModel;
using System.Reflection;

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
        IDataTypeMapper dataTypeMapper)
    {
        var filterable =
            property.GetCustomAttribute<FilterableAttribute>();

        var searchable =
            property.GetCustomAttribute<SearchableAttribute>();

        var sortable =
            property.GetCustomAttribute<SortableAttribute>();

        var description =
            property.GetCustomAttribute<DescriptionAttribute>();

        return new FieldMetadata(
            property.Name,
            description?.Description,
            property.PropertyType,
            dataTypeMapper.GetDescriptor(property),
            filterable is not null,
            filterable?.Operators ?? [],
            searchable is not null,
            searchable?.Priority,
            searchable?.MatchMode,
            sortable is not null);
    }

    /// <summary>
    /// Derives parameter metadata for each public instance property of a view-parameters
    /// type. <see cref="EmptyQueryViewParameters"/> yields an empty list.
    /// </summary>
    internal static IReadOnlyList<QueryParameterMetadata> ToParameterMetadata(
        this Type parametersType,
        IDataTypeMapper dataTypeMapper,
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
                new QueryParameterMetadata(
                    property.Name,
                    property.PropertyType,
                    dataTypeMapper.GetDescriptor(property),
                    constraintMapper.Map(property),
                    property.GetCustomAttribute<DescriptionAttribute>()?.Description))
            .ToArray();
    }

    /// <summary>
    /// Derives output-field metadata for each public instance property of a view type.
    /// </summary>
    internal static IReadOnlyList<QueryOutputFieldMetadata> ToOutputFieldMetadata(
        this Type viewType,
        IDataTypeMapper dataTypeMapper) =>
        viewType
            .GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance)
            .Select(property =>
                new QueryOutputFieldMetadata(
                    property.Name,
                    property.GetCustomAttribute<DescriptionAttribute>()?.Description,
                    property.PropertyType,
                    dataTypeMapper.GetDescriptor(property)))
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
        IDataTypeMapper dataTypeMapper,
        IConstraintMapper constraintMapper) =>
        new(
            attribute.Name,
            attribute.Version,
            attribute.DisplayName ?? attribute.Name,
            attribute.Description
                ?? attribute.DisplayName
                ?? attribute.Name,
            attribute.Visibility,
            queryViewType.ToViewPageable(contextType, attribute),
            parametersType.ToParameterMetadata(dataTypeMapper, constraintMapper),
            viewType.ToOutputFieldMetadata(dataTypeMapper));

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

        if (pageable is null)
        {
            return null;
        }

        ValidateDefaultSort(contextType, attribute);

        return new PageableMetadata(
            pageable.DefaultSize,
            pageable.MaxSize);
    }

    private static void ValidateDefaultSort(
        Type contextType,
        QueryViewAttribute attribute)
    {
        if (string.IsNullOrWhiteSpace(attribute.DefaultSortField))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.QryInvalidRegistration,
                $"Query view '{attribute.Name}' is pageable and must define a DefaultSortField.");
        }

        var property =
            contextType.GetProperty(
                attribute.DefaultSortField,
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.IgnoreCase);

        if (property is null)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.QryInvalidRegistration,
                $"Query view '{attribute.Name}' specifies DefaultSortField '{attribute.DefaultSortField}' which does not exist on query context '{contextType.Name}'.");
        }

        if (property.GetCustomAttribute<SortableAttribute>() is null)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.QryInvalidRegistration,
                $"Query view '{attribute.Name}' specifies DefaultSortField '{attribute.DefaultSortField}' but the field is not marked as sortable.");
        }
    }
}
