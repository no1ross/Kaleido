using Kaleido;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

internal sealed class QueryViewRegistry
    : IQueryViewRegistry
{
    private readonly IReadOnlyDictionary<string, QueryViewRegistration> _byName;
    private readonly IReadOnlyDictionary<Type, QueryViewRegistration> _byType;
    private readonly IReadOnlyCollection<QueryViewRegistration> _registrations;

    public QueryViewRegistry(
        IServiceCollection services,
        IEnumerable<Type> queryViewTypes)
    {
        var registrations =
            queryViewTypes
                .Select(x =>
                    BuildRegistration(x))
                .ToArray();

        _registrations =
            registrations;

        _byName =
            registrations.ToDictionary(
                x => x.Metadata.Name,
                StringComparer.OrdinalIgnoreCase);

        _byType =
            registrations.ToDictionary(
                x => x.QueryViewType);
    }

    public IReadOnlyCollection<QueryViewRegistration> Registrations =>
        _registrations;

    public IReadOnlyCollection<QueryViewRegistration> GetAll() =>
        _registrations;

    public QueryViewRegistration? Find(string name)
    {
        _byName.TryGetValue(
            name,
            out var registration);

        return registration;
    }

    public QueryViewRegistration? Find(Type queryViewType)
    {
        _byType.TryGetValue(
            queryViewType,
            out var registration);

        return registration;
    }

    public QueryViewRegistration GetRegistration(string name)
    {
        return Find(name)
            ?? throw new KeyNotFoundException(
                $"Query view '{name}' is not registered.");
    }

    public QueryViewRegistration GetRegistration(Type queryViewType)
    {
        return Find(queryViewType)
            ?? throw new KeyNotFoundException(
                $"Query view '{queryViewType.FullName}' is not registered.");
    }

    private static QueryViewRegistration BuildRegistration(
        Type queryViewType)
    {
        var queryViewAttribute =
            queryViewType.GetCustomAttribute<QueryViewAttribute>()
            ?? throw new InvalidOperationException(
                $"Query view '{queryViewType.Name}' is missing QueryViewAttribute.");

        var queryViewInterface =
            GetQueryViewInterface(
                queryViewType);

        var contextType =
            queryViewInterface.GenericTypeArguments[0];

        var sourceType =
            queryViewInterface.GenericTypeArguments[1];

        var parametersType =
            queryViewInterface.GenericTypeArguments.Length == 3
                ? queryViewInterface.GenericTypeArguments[2]
                : typeof(EmptyQueryViewParameters);

        var pageable =
            BuildPageable(
                queryViewType,
                contextType,
                queryViewAttribute);

        return new QueryViewRegistration(
            queryViewType,
            sourceType,
            parametersType,
            contextType,
            new QueryViewMetadata(
                queryViewAttribute.Name,
                queryViewAttribute.Version,
                queryViewAttribute.DisplayName ?? queryViewAttribute.Name,
                queryViewAttribute.Description
                    ?? queryViewAttribute.DisplayName
                    ?? queryViewAttribute.Name,
                queryViewAttribute.Visibility,
                pageable,
                BuildParameters(parametersType),
                BuildOutputFields(sourceType)));
    }

    private static IReadOnlyList<QueryParameterMetadata> BuildParameters(
        Type parametersType)
    {
        if (parametersType == typeof(EmptyQueryViewParameters))
        {
            return Array.Empty<QueryParameterMetadata>();
        }

        return parametersType
            .GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance)
            .Select(property =>
                new QueryParameterMetadata(
                    property.Name,
                    property.PropertyType,
                    DataTypeMapper.GetDescriptor(property.PropertyType),
                    ConstraintMapper.Map(property),
                    property.GetCustomAttribute<DescriptionAttribute>()?.Description))
            .ToArray();
    }

    private static IReadOnlyList<QueryOutputFieldMetadata> BuildOutputFields(
        Type viewType)
    {
        return viewType
            .GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance)
            .Select(property =>
                new QueryOutputFieldMetadata(
                    property.Name,
                    property.GetCustomAttribute<DescriptionAttribute>()?.Description,
                    property.PropertyType,
                    DataTypeMapper.GetDescriptor(property.PropertyType)))
            .ToArray();
    }

    private static PageableMetadata? BuildPageable(
        Type queryViewType,
        Type contextType,
        QueryViewAttribute attribute)
    {
        var pageable =
            queryViewType.GetCustomAttribute<PageableAttribute>();

        if (pageable is null)
        {
            return null;
        }

        ValidateDefaultSort(
            contextType,
            attribute);

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
            throw new InvalidOperationException(
                $"Query view '{attribute.Name}' is pageable and must define a DefaultSortField.");
        }

        var property =
            contextType.GetProperty(
                attribute.DefaultSortField,
                BindingFlags.Public |
                BindingFlags.Instance|
                BindingFlags.IgnoreCase);

        if (property is null)
        {
            throw new InvalidOperationException(
                $"Query view '{attribute.Name}' specifies DefaultSortField '{attribute.DefaultSortField}' which does not exist on query context '{contextType.Name}'.");
        }

        if (property.GetCustomAttribute<SortableAttribute>() is null)
        {
            throw new InvalidOperationException(
                $"Query view '{attribute.Name}' specifies DefaultSortField '{attribute.DefaultSortField}' but the field is not marked as sortable.");
        }
    }


    private static Type GetQueryViewInterface(
        Type queryViewType)
    {
        return queryViewType
            .GetInterfaces()
            .Where(i =>
                i.IsGenericType &&
                (
                    i.GetGenericTypeDefinition() ==
                        typeof(IQueryViewSource<,>) ||

                    i.GetGenericTypeDefinition() ==
                        typeof(IQueryViewSource<,,>)
                ))
            .OrderByDescending(
                i => i.GenericTypeArguments.Length)
            .First();
    }

}
