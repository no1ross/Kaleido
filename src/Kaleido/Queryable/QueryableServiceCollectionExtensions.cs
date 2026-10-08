using System.Reflection;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Observability;
using Kaleido.Queryable.Registry;
using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Queryable;

public static class QueryableServiceCollectionExtensions
{
    internal static IKaleidoBuilder AddQueryable(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Assemblies.Count == 0)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.MissingAssembly,
                "At least one assembly must be registered before AddQueryable().");
        }

        var (localSourceTypes, delegatedSourceTypes, viewTypes) =
            builder.Assemblies.ScanTypes()
                .Where(x =>
                    x.PassesTypeFilter(
                        builder.ServiceOptions.TypeFilter,
                        QueryableErrorCodes.InvalidRegistration,
                        "queryable type"))
                .DiscoverQueryableCapabilities();

        if (localSourceTypes.Length + delegatedSourceTypes.Length + viewTypes.Length == 0)
        {
            // No query sources to register - this is valid for Process-only services
            return builder;
        }

        foreach (var type in localSourceTypes.Concat(delegatedSourceTypes).Concat(viewTypes))
        {
            builder.Services.TryAddScoped(type);
        }

        builder.Services.TryAddSingleton<QuerySourceRegistry>(
            sp => new QuerySourceRegistry(
                sp.GetRequiredService<ITypeDescriber>(),
                sp.GetRequiredService<IConstraintMapper>(),
                localSourceTypes,
                delegatedSourceTypes,
                builder.ServiceOptions.DefaultAuthorization));

        builder.Services.TryAddSingleton<QueryViewRegistry>(
            sp => new QueryViewRegistry(
                sp.GetRequiredService<ITypeDescriber>(),
                sp.GetRequiredService<IConstraintMapper>(),
                viewTypes));

        builder.Services.TryAddSingleton<IQuerySourceRegistry>(sp => sp.GetRequiredService<QuerySourceRegistry>());
        builder.Services.TryAddSingleton<IQueryViewRegistry>(sp => sp.GetRequiredService<QueryViewRegistry>());

        builder.Services.TryAddSingleton<IQueryableRegistry, QueryableRegistry>();

        RegisterFrameworkServices(builder.Services);

        return builder;
    }

    private static void RegisterFrameworkServices(IServiceCollection services)
    {
        services.TryAddSingleton<IQueryContextValidator, QueryRequestValidator>();
        services.TryAddSingleton<IQueryContextCompiler, QueryRequestCompiler>();
        services.TryAddSingleton<IQueryableService, QueryableService>();

        services.TryAddSingleton(
            typeof(ICompiledQueryApplier<>),
            typeof(CompiledQueryApplier<>));

        services.TryAddSingleton(
            typeof(IQueryContextExecutor<>),
            typeof(QueryContextExecutor<>));

        services.TryAddScoped(
            typeof(IQueryContextEngine<,>),
            typeof(QueryContextEngine<,>));

        services.TryAddScoped(
            typeof(IDelegatedQuerySourceEngine<,>),
            typeof(DelegatedQuerySourceEngine<,>));

        services.TryAddSingleton<IQueryEventFactory, QueryEventFactory>();
        services.TryAddScoped<IQueryableObservability, QueryableObservability>();
    }

    /// <summary>
    /// Discovers and validates the Queryable capabilities among <paramref name="types"/>.
    /// Discovery is interface-only — the interfaces are the identity; each discovered
    /// capability must then carry its attribute with complete metadata.
    /// </summary>
    /// <exception cref="KaleidoConfigurationException">A discovered capability is invalid.</exception>
    internal static (Type[] LocalSources, Type[] DelegatedSources, Type[] Views) DiscoverQueryableCapabilities(
        this IEnumerable<Type> types)
    {
        var candidateTypes =
            types
                .Where(x =>
                    x.IsLocalQuerySource() ||
                    x.IsDelegatedQuerySource() ||
                    x.IsQueryView())
                .ToArray();

        var localSourceTypes = candidateTypes.Where(x => x.IsLocalQuerySource()).ToArray();
        var delegatedSourceTypes = candidateTypes.Where(x => x.IsDelegatedQuerySource()).ToArray();
        var viewTypes = candidateTypes.Where(x => x.IsQueryView()).ToArray();

        ValidateSources(localSourceTypes, delegatedSourceTypes, viewTypes);
        ValidateViews(viewTypes, localSourceTypes);

        return (localSourceTypes, delegatedSourceTypes, viewTypes);
    }

    private static void ValidateSources(
        IReadOnlyCollection<Type> localSourceTypes,
        IReadOnlyCollection<Type> delegatedSourceTypes,
        IReadOnlyCollection<Type> viewTypes)
    {
        foreach (var type in localSourceTypes)
        {
            var sourceInterfaceCount =
                type.GetSyncSourceInterfaces().Length +
                type.GetAsyncSourceInterfaces().Length;

            if (sourceInterfaceCount == 0)
            {
                throw new KaleidoConfigurationException(
                    QueryableErrorCodes.InvalidRegistration,
                    $"Query source '{type.FullName}' implements ILocalQuerySource<T> only. " +
                    "Implement exactly one of IQuerySource<TQueryContext> or IQuerySourceAsync<TQueryContext>.");
            }

            if (sourceInterfaceCount > 1)
            {
                throw new KaleidoConfigurationException(
                    QueryableErrorCodes.InvalidRegistration,
                    $"Query source '{type.FullName}' implements more than one source interface. " +
                    "Implement exactly one of IQuerySource<TQueryContext> or IQuerySourceAsync<TQueryContext>, for one query context.");
            }
        }

        foreach (var type in delegatedSourceTypes)
        {
            if (type.IsLocalQuerySource())
            {
                throw new KaleidoConfigurationException(
                    QueryableErrorCodes.InvalidRegistration,
                    $"Query source '{type.FullName}' is both a local and a delegated source. Implement exactly one.");
            }

            if (type.GetDelegatedSourceInterfaces().Length > 1)
            {
                throw new KaleidoConfigurationException(
                    QueryableErrorCodes.InvalidRegistration,
                    $"Delegated query source '{type.FullName}' implements more than one IDelegatedQuerySource interface. Implement exactly one.");
            }
        }

        var sourceTypes = localSourceTypes.Concat(delegatedSourceTypes).ToArray();

        if (sourceTypes.Intersect(viewTypes).FirstOrDefault() is { } sourceAndView)
        {
            throw new KaleidoConfigurationException(
                QueryableErrorCodes.InvalidRegistration,
                $"Type '{sourceAndView.FullName}' is both a query source and a query view. A type is exactly one capability.");
        }

        foreach (var type in sourceTypes)
        {
            var attribute =
                type.GetCustomAttribute<QuerySourceAttribute>()
                ?? throw new KaleidoConfigurationException(
                    QueryableErrorCodes.MissingAttribute,
                    $"Query source '{type.FullName}' must be decorated with [QuerySource].");

            RequireNonEmpty(type, "Query source", attribute.Version, nameof(QuerySourceAttribute.Version));
            RequireNonEmpty(type, "Query source", attribute.DisplayName, nameof(QuerySourceAttribute.DisplayName));
            RequireNonEmpty(type, "Query source", attribute.Description, nameof(QuerySourceAttribute.Description));
        }

        var duplicates =
            sourceTypes
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .ToArray();

        if (duplicates.Length > 0)
        {
            throw new KaleidoConfigurationException(
                QueryableErrorCodes.DuplicateRegistration,
                "Duplicate query source names detected (a source's name is its type name): " +
                string.Join("; ", duplicates.Select(g => $"'{g.Key}' ({string.Join(", ", g.Select(t => t.FullName))})")));
        }
    }

    private static void ValidateViews(
        IReadOnlyCollection<Type> viewTypes,
        IReadOnlyCollection<Type> localSourceTypes)
    {
        var registeredSources = localSourceTypes.ToHashSet();

        foreach (var type in viewTypes)
        {
            var syncCount = type.GetSyncViewInterfaces().Length;
            var asyncCount = type.GetAsyncViewInterfaces().Length;

            if (syncCount > 0 && asyncCount > 0)
            {
                throw new KaleidoConfigurationException(
                    QueryableErrorCodes.InvalidRegistration,
                    $"Query view '{type.FullName}' implements both IQueryViewSource and IQueryViewSourceAsync. Implement exactly one.");
            }

            if (syncCount + asyncCount > 1)
            {
                throw new KaleidoConfigurationException(
                    QueryableErrorCodes.InvalidRegistration,
                    $"Query view '{type.FullName}' implements more than one view interface. A view projects exactly one source.");
            }

            var attribute =
                type.GetCustomAttribute<QueryViewAttribute>()
                ?? throw new KaleidoConfigurationException(
                    QueryableErrorCodes.MissingAttribute,
                    $"Query view '{type.FullName}' must be decorated with [QueryView].");

            RequireNonEmpty(type, "Query view", attribute.Version, nameof(QueryViewAttribute.Version));
            RequireNonEmpty(type, "Query view", attribute.DisplayName, nameof(QueryViewAttribute.DisplayName));
            RequireNonEmpty(type, "Query view", attribute.Description, nameof(QueryViewAttribute.Description));

            var sourceType = type.GetViewInterfaces()[0].GenericTypeArguments[0];

            if (!registeredSources.Contains(sourceType))
            {
                throw new KaleidoConfigurationException(
                    QueryableErrorCodes.MissingSource,
                    $"Query view '{type.FullName}' references query source '{sourceType.FullName}', which is not a registered local query source.");
            }
        }

        var duplicates =
            viewTypes
                .GroupBy(x => (Source: x.GetViewInterfaces()[0].GenericTypeArguments[0], Name: x.Name.ToUpperInvariant()))
                .Where(x => x.Count() > 1)
                .ToArray();

        if (duplicates.Length > 0)
        {
            throw new KaleidoConfigurationException(
                QueryableErrorCodes.DuplicateRegistration,
                "Duplicate query view names detected within a source (a view's name is its type name): " +
                string.Join("; ", duplicates.Select(g => $"'{g.First().Name}' on '{g.Key.Source.Name}'")));
        }
    }

    private static void RequireNonEmpty(
        Type type,
        string kind,
        string? value,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new KaleidoConfigurationException(
                QueryableErrorCodes.MissingAttribute,
                $"{kind} '{type.FullName}' must specify a non-empty {propertyName}.");
        }
    }
}
