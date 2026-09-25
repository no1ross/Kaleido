using System.Reflection;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Registry;

public interface IDelegatedQueryViewRegistry
{
    IReadOnlyCollection<DelegatedQueryViewRegistration> Registrations { get; }

    DelegatedQueryViewRegistration? Find(string name);

    DelegatedQueryViewRegistration? Find(Type recordType);

    DelegatedQueryViewRegistration GetRegistration(string name);

    DelegatedQueryViewRegistration GetRegistration(Type recordType);
}

internal sealed class DelegatedQueryViewRegistry : IDelegatedQueryViewRegistry
{
    private readonly IReadOnlyCollection<DelegatedQueryViewRegistration> _registrations;
    private readonly IReadOnlyDictionary<string, DelegatedQueryViewRegistration> _byName;
    private readonly IReadOnlyDictionary<Type, DelegatedQueryViewRegistration> _byType;

    private readonly IDataTypeMapper _dataTypeMapper;
    private readonly IConstraintMapper _constraintMapper;

    public DelegatedQueryViewRegistry(
        IDataTypeMapper dataTypeMapper,
        IConstraintMapper constraintMapper,
        IEnumerable<Type> queryViewTypes)
    {
        ArgumentNullException.ThrowIfNull(dataTypeMapper);
        ArgumentNullException.ThrowIfNull(constraintMapper);
        ArgumentNullException.ThrowIfNull(queryViewTypes);

        _dataTypeMapper = dataTypeMapper;
        _constraintMapper = constraintMapper;

        _registrations =
            queryViewTypes
                .Select(BuildRegistration)
                .ToArray();

        _byName =
            _registrations.ToDictionary(
                x => x.ViewMetadata.Name,
                StringComparer.OrdinalIgnoreCase);

        _byType =
            _registrations.ToDictionary(
                x => x.QueryViewType);
    }

    public IReadOnlyCollection<DelegatedQueryViewRegistration> Registrations =>
        _registrations;

    public DelegatedQueryViewRegistration? Find(string name)
    {
        _byName.TryGetValue(name, out var registration);
        return registration;
    }

    public DelegatedQueryViewRegistration? Find(Type recordType)
    {
        _byType.TryGetValue(recordType, out var registration);
        return registration;
    }

    public DelegatedQueryViewRegistration GetRegistration(string name) =>
        Find(name)
        ?? throw new KaleidoFrameworkException(
            FrameworkErrorCodes.MissingRegistration,
            $"Delegated query view '{name}' is not registered.");

    public DelegatedQueryViewRegistration GetRegistration(Type recordType) =>
        Find(recordType)
        ?? throw new KaleidoFrameworkException(
            FrameworkErrorCodes.MissingRegistration,
            $"Delegated query view '{recordType.FullName}' is not registered.");

    private DelegatedQueryViewRegistration BuildRegistration(Type queryViewType)
    {
        var queryViewAttribute =
            queryViewType.GetCustomAttribute<QueryViewAttribute>()
            ?? throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.QryMissingAttribute,
                $"Query view '{queryViewType.Name}' is missing QueryViewAttribute.");

        var queryViewInterface =
            queryViewType
                .GetInterfaces()
                .Where(i =>
                    i.IsGenericType &&
                    (
                        i.GetGenericTypeDefinition() == typeof(IDelegateQueryViewSource<,>) ||
                        i.GetGenericTypeDefinition() == typeof(IDelegateQueryViewSource<,,>)
                    ))
                .OrderByDescending(i => i.GenericTypeArguments.Length)
                .First();

        var contextType = queryViewInterface.GenericTypeArguments[0];
        var viewType = queryViewInterface.GenericTypeArguments[1];
        var parametersType =
            queryViewInterface.GenericTypeArguments.Length == 3
                ? queryViewInterface.GenericTypeArguments[2]
                : typeof(EmptyQueryViewParameters);

        return new DelegatedQueryViewRegistration(
            queryViewType,
            viewType,
            parametersType,
            contextType,
            BuildQueryMetadata(contextType),
            new QueryViewMetadata(
                queryViewAttribute.Name,
                queryViewAttribute.Version,
                queryViewAttribute.DisplayName ?? queryViewAttribute.Name,
                queryViewAttribute.Description ?? queryViewAttribute.DisplayName ?? queryViewAttribute.Name,
                queryViewAttribute.Visibility,
                BuildPageable(queryViewType, contextType, queryViewAttribute),
                BuildParameters(parametersType),
                BuildOutputFields(viewType)));
    }

    private QueryContextMetadata BuildQueryMetadata(Type contextType)
    {
        var attribute =
            contextType.GetCustomAttribute<QueryContextAttribute>()
            ?? throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.QryMissingAttribute,
                $"Delegated query view context '{contextType.Name}' is missing QueryContextAttribute.");

        var pageable =
            contextType.GetCustomAttribute<PageableAttribute>() is PageableAttribute pageableAttribute
                ? new PageableMetadata(pageableAttribute.DefaultSize, pageableAttribute.MaxSize)
                : null;

        return new QueryContextMetadata(
            attribute.Name,
            attribute.Description ?? attribute.DisplayName ?? attribute.Name,
            attribute.DisplayName ?? attribute.Name,
            attribute.Version,
            attribute.Source,
            attribute.Kind,
            pageable,
            contextType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(BuildField)
                .ToArray());
    }

    private IReadOnlyList<QueryParameterMetadata> BuildParameters(Type parametersType)
    {
        if (parametersType == typeof(EmptyQueryViewParameters))
        {
            return Array.Empty<QueryParameterMetadata>();
        }

        return parametersType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property =>
                new QueryParameterMetadata(
                    property.Name,
                    property.PropertyType,
                    _dataTypeMapper.GetDescriptor(property),
                    _constraintMapper.Map(property),
                    property.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description))
            .ToArray();
    }

    private IReadOnlyList<QueryOutputFieldMetadata> BuildOutputFields(Type viewType)
    {
        return viewType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property =>
                new QueryOutputFieldMetadata(
                    property.Name,
                    property.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description,
                    property.PropertyType,
                    _dataTypeMapper.GetDescriptor(property)))
            .ToArray();
    }

    private static PageableMetadata? BuildPageable(Type queryViewType, Type contextType, QueryViewAttribute attribute)
    {
        var pageable = queryViewType.GetCustomAttribute<PageableAttribute>();
        if (pageable is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(attribute.DefaultSortField))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.QryInvalidRegistration,
                $"Query view '{attribute.Name}' is pageable and must define a DefaultSortField.");
        }

        var property =
            contextType.GetProperty(
                attribute.DefaultSortField,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

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

        return new PageableMetadata(pageable.DefaultSize, pageable.MaxSize);
    }

    private FieldMetadata BuildField(PropertyInfo property)
    {
        var filterable = property.GetCustomAttribute<FilterableAttribute>();
        var searchable = property.GetCustomAttribute<SearchableAttribute>();
        var sortable = property.GetCustomAttribute<SortableAttribute>();
        var description = property.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();

        return new FieldMetadata(
            property.Name,
            description?.Description,
            property.PropertyType,
            _dataTypeMapper.GetDescriptor(property),
            filterable is not null,
            filterable?.Operators ?? Array.Empty<FilterOperator>(),
            searchable is not null,
            searchable?.Priority,
            searchable?.MatchMode,
            sortable is not null);
    }
}
