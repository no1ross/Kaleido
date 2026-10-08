using System.Reflection;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Registry;

internal interface IQueryViewRegistry
{
    IReadOnlyCollection<QueryViewRegistration> Registrations { get; }

    QueryViewRegistration? Find(Type queryViewType);

    QueryViewRegistration GetRegistration(Type queryViewType);
}

/// <summary>
/// The discovered local query views, keyed by view type. View names are unique per source, not
/// per service, so views are not looked up by name here.
/// </summary>
internal sealed class QueryViewRegistry
    : IQueryViewRegistry
{
    private readonly IReadOnlyDictionary<Type, QueryViewRegistration> _byType;
    private readonly IReadOnlyCollection<QueryViewRegistration> _registrations;

    private readonly ITypeDescriber _typeDescriber;
    private readonly IConstraintMapper _constraintMapper;

    public QueryViewRegistry(
        ITypeDescriber typeDescriber,
        IConstraintMapper constraintMapper,
        IEnumerable<Type> queryViewTypes)
    {
        ArgumentNullException.ThrowIfNull(typeDescriber);
        ArgumentNullException.ThrowIfNull(constraintMapper);
        ArgumentNullException.ThrowIfNull(queryViewTypes);

        _typeDescriber = typeDescriber;
        _constraintMapper = constraintMapper;

        var registrations =
            queryViewTypes
                .Select(BuildRegistration)
                .ToArray();

        _registrations =
            registrations;

        _byType =
            registrations.ToDictionary(
                x => x.QueryViewType);
    }

    public IReadOnlyCollection<QueryViewRegistration> Registrations =>
        _registrations;

    public QueryViewRegistration? Find(Type queryViewType)
    {
        _byType.TryGetValue(
            queryViewType,
            out var registration);

        return registration;
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
                QueryableErrorCodes.MissingAttribute,
                $"Query view '{queryViewType.FullName}' must be decorated with [QueryView].");

        // Generic arguments: source, query context, view, parameters.
        var arguments =
            queryViewType.GetViewInterfaces()
                .Single()
                .GenericTypeArguments;

        var sourceType = arguments[0];
        var contextType = arguments[1];
        var viewType = arguments[2];
        var parametersType = arguments[3];

        return new QueryViewRegistration(
            queryViewType,
            viewType,
            parametersType,
            sourceType,
            contextType,
            queryViewAttribute.ToViewMetadata(
                queryViewType,
                contextType,
                parametersType,
                viewType,
                _typeDescriber,
                _constraintMapper));
    }
}
