using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Metadata;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Queryable.Records;

internal sealed class RecordRegistry : IRecordRegistry
{
    private readonly IReadOnlyDictionary<string, RecordRegistration> _byName;
    private readonly IReadOnlyDictionary<Type, RecordRegistration> _byType;
    private readonly IReadOnlyCollection<RecordRegistration> _registrations;

    public RecordRegistry(
        IServiceCollection services,
        IEnumerable<Type> recordTypes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(recordTypes);

        var registrations =
            recordTypes
                .Select(recordType =>
                    BuildRegistration(
                        services,
                        recordType))
                .ToArray();

        _registrations = registrations;

        _byName =
            registrations.ToDictionary(
                x => x.Metadata.Name,
                StringComparer.OrdinalIgnoreCase);

        _byType =
            registrations.ToDictionary(
                x => x.RecordType);
    }

    public IReadOnlyCollection<RecordRegistration> Registrations =>
        _registrations;

    public IReadOnlyCollection<RecordRegistration> GetAll() =>
        _registrations;

    public RecordRegistration? Find(string name)
    {
        _byName.TryGetValue(
            name,
            out var registration);

        return registration;
    }

    public RecordRegistration? Find(Type recordType)
    {
        _byType.TryGetValue(
            recordType,
            out var registration);

        return registration;
    }

    public RecordRegistration GetRegistration(string name)
    {
        return Find(name)
            ?? throw new KeyNotFoundException(
                $"Record '{name}' is not registered.");
    }

    public RecordRegistration GetRegistration(Type recordType)
    {
        return Find(recordType)
            ?? throw new KeyNotFoundException(
                $"Record type '{recordType.FullName}' is not registered.");
    }

    private static RecordRegistration BuildRegistration(
        IServiceCollection services,
        Type recordType)
    {
        var sourceType =
            GetSourceType(
                services,
                recordType);

        var metadata =
            BuildRecordMetadata(
                recordType);

        var namedQueries =
            GetNamedQueries(
                services,
                recordType);

        return new RecordRegistration(
            recordType,
            sourceType,
            metadata,
            namedQueries);
    }

    private static Type GetSourceType(
        IServiceCollection services,
        Type recordType)
    {
        var sourceInterface =
            typeof(IRecordSource<>)
                .MakeGenericType(recordType);

        var source =
            services.Single(
                x => x.ServiceType == sourceInterface);

        return source.ImplementationType
            ?? throw new InvalidOperationException(
                $"No implementation type registered for source '{sourceInterface.Name}'.");
    }

    private static IReadOnlyCollection<NamedQueryRegistration>
        GetNamedQueries(
            IServiceCollection services,
            Type recordType)
    {
        var queryInterface =
            typeof(IRecordNamedQuery<>)
                .MakeGenericType(recordType);

        return services
            .Where(x => x.ServiceType == queryInterface)
            .Select(BuildNamedQueryRegistration)
            .ToArray();
    }

    private static NamedQueryRegistration BuildNamedQueryRegistration(
        ServiceDescriptor descriptor)
    {
        var queryType =
            descriptor.ImplementationType
            ?? throw new InvalidOperationException(
                $"Named query registration '{descriptor.ServiceType.Name}' is missing an implementation type.");

        var queryAttribute =
            queryType.GetCustomAttribute<NamedQueryAttribute>()
            ?? throw new InvalidOperationException(
                $"Named query '{queryType.Name}' is missing NamedQueryAttribute.");

        var parameters =
            queryType
                .GetCustomAttributes<NamedQueryParameterAttribute>()
                .Select(x =>
                    new QueryParameterMetadata(
                        x.Name,
                        x.ParameterType,
                        x.Required,
                        x.Description,
                        x.DefaultValue))
                .ToArray();

        return new NamedQueryRegistration(
            queryType,
            new NamedQueryMetadata(
                queryAttribute.Name,
                queryAttribute.Description,
                parameters));
    }

    private static RecordMetadata BuildRecordMetadata(
        Type recordType)
    {
        var attribute =
            recordType.GetCustomAttribute<QueryableRecordAttribute>()
            ?? throw new InvalidOperationException(
                $"Record '{recordType.Name}' is missing KaleidoRecordAttribute.");

        return new RecordMetadata(
            attribute.Name,
            attribute.Description,
            attribute.Version,
            attribute.Source,
            recordType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(BuildField)
                .ToArray(),
            BuildPageable(recordType));
    }

    private static FieldMetadata BuildField(
        PropertyInfo property)
    {
        var filterable =
            property.GetCustomAttribute<FilterableAttribute>();

        var searchable =
            property.GetCustomAttribute<SearchableAttribute>();

        var sortable =
            property.GetCustomAttribute<SortableAttribute>();

        return new FieldMetadata(
            property.Name,
            property.PropertyType,
            filterable is not null,
            filterable?.Operators ?? Array.Empty<FilterOperator>(),
            searchable is not null,
            searchable?.Priority,
            searchable?.MatchModes ?? Array.Empty<MatchMode>(),
            sortable is not null);
    }

    private static PageableMetadata? BuildPageable(
        Type recordType)
    {
        var pageable =
            recordType.GetCustomAttribute<PageableAttribute>();

        if (pageable is null)
        {
            return null;
        }

        return new PageableMetadata(
            pageable.DefaultSize,
            pageable.MaxSize);
    }
}