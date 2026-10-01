using System.Reflection;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Registry;

internal interface IQueryViewRegistry
{
    IReadOnlyCollection<QueryViewRegistration> Registrations { get; }

    QueryViewRegistration? Find(string name);

    QueryViewRegistration? Find(Type recordType);

    QueryViewRegistration GetRegistration(string name);

    QueryViewRegistration GetRegistration(Type recordType);
}

internal sealed class QueryViewRegistry
    : IQueryViewRegistry
{
    private readonly IReadOnlyDictionary<string, QueryViewRegistration> _byName;
    private readonly IReadOnlyDictionary<Type, QueryViewRegistration> _byType;
    private readonly IReadOnlyCollection<QueryViewRegistration> _registrations;

    private readonly ITypeDescriber _dataTypeMapper;
    private readonly IConstraintMapper _constraintMapper;

    public QueryViewRegistry(
        ITypeDescriber TypeDescriber,
        IConstraintMapper constraintMapper,
        IEnumerable<Type> queryViewTypes)
    {
        ArgumentNullException.ThrowIfNull(TypeDescriber);
        ArgumentNullException.ThrowIfNull(constraintMapper);
        ArgumentNullException.ThrowIfNull(queryViewTypes);

        _dataTypeMapper = TypeDescriber;
        _constraintMapper = constraintMapper;

        var registrations =
            queryViewTypes
                .Select(BuildRegistration)
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
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                $"Query view '{name}' is not registered.");
    }

    public QueryViewRegistration GetRegistration(Type queryViewType)
    {
        return Find(queryViewType)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                $"Query view '{queryViewType.FullName}' is not registered.");
    }

    private QueryViewRegistration BuildRegistration(
        Type queryViewType)
    {
        var queryViewAttribute =
            queryViewType.GetCustomAttribute<QueryViewAttribute>()
            ?? throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.QryMissingAttribute,
                $"Query view '{queryViewType.Name}' is missing QueryViewAttribute.");

        var queryViewInterface =
            queryViewType.GetQueryViewInterface();

        var contextType =
            queryViewInterface.GenericTypeArguments[0];

        var sourceType =
            queryViewInterface.GenericTypeArguments[1];

        var parametersType =
            queryViewInterface.GenericTypeArguments.Length == 3
                ? queryViewInterface.GenericTypeArguments[2]
                : typeof(EmptyQueryViewParameters);

        return new QueryViewRegistration(
            queryViewType,
            sourceType,
            parametersType,
            contextType,
            queryViewAttribute.ToViewMetadata(
                queryViewType,
                contextType,
                parametersType,
                sourceType,
                _dataTypeMapper,
                _constraintMapper));
    }
}
