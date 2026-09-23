using System.Reflection;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Observability;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
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

        var types = builder.Assemblies.ScanTypes();

        var queryContextTypes =
            types
                .Where(x =>
                    x.GetCustomAttribute<QueryContextAttribute>() is not null)
                .Where(x =>
                    x.PassesTypeFilter(
                        builder.ServiceOptions.TypeFilter,
                        ConfigurationErrorCodes.QryInvalidRegistration,
                        "queryable type"))
                .ToArray();

        if (queryContextTypes.Length == 0)
        {
            // No query contexts to register - this is valid for Process-only services
            return builder;
        }

        var delegatedContextTypes =
            queryContextTypes
                .Where(x => x.GetCustomAttribute<QueryContextAttribute>()?.Kind == QueryContextKind.Delegated)
                .ToArray();

        var localContextTypes =
            queryContextTypes
                .Except(delegatedContextTypes)
                .ToArray();

        foreach (var contextType in localContextTypes)
        {
            RegisterSource(
                builder.Services,
                contextType,
                types);

            RegisterContextEngines(
                builder.Services,
                contextType,
                types);
        }

        var queryViewTypes =
            types
                .Where(x =>
                    x.GetCustomAttribute<QueryViewAttribute>() is not null)
                .Where(x =>
                    x.PassesTypeFilter(
                        builder.ServiceOptions.TypeFilter,
                        ConfigurationErrorCodes.QryInvalidRegistration,
                        "queryable type"))
                .ToArray();

        var delegatedQueryViewTypes =
            queryViewTypes
                .Where(IsDelegatedQueryView)
                .ToArray();

        var localQueryViewTypes =
            queryViewTypes
                .Except(delegatedQueryViewTypes)
                .ToArray();

        foreach (var viewType in localQueryViewTypes)
        {
            RegisterQueryView(
                builder.Services,
                viewType);
        }

        foreach (var viewType in delegatedQueryViewTypes)
        {
            RegisterDelegatedQueryView(
                builder.Services,
                viewType);
        }

        builder.Services.TryAddSingleton<QueryContextRegistrationValidator>();

        builder.Services.TryAddSingleton<IQueryContextRegistry>(
            sp =>
            {
                var validator =
                    sp.GetRequiredService<QueryContextRegistrationValidator>();

                validator.Validate(
                    localContextTypes,
                    builder.Services);

                return new QueryContextRegistry(
                    builder.Services,
                    localContextTypes);
            });

        builder.Services.TryAddSingleton<QueryViewRegistrationValidator>();

        builder.Services.TryAddSingleton<IQueryViewRegistry>(
            sp =>
            {
                var validator =
                    sp.GetRequiredService<QueryViewRegistrationValidator>();

                validator.Validate(
                    localQueryViewTypes,
                    localContextTypes,
                    builder.Services);

                return new QueryViewRegistry(
                    localQueryViewTypes);
            });

        builder.Services.TryAddSingleton<IDelegatedQueryViewRegistry>(
            _ => new DelegatedQueryViewRegistry(
                delegatedQueryViewTypes));

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

        services.TryAddSingleton<IQueryEventFactory, QueryEventFactory>();
        services.TryAddScoped<IQueryableObservability, QueryableObservability>();
    }

    private static void RegisterSource(
        IServiceCollection services,
        Type contextType,
        IEnumerable<Type> types)
    {
        var typeList = types as Type[] ?? types.ToArray();

        var syncSources =
            typeList
                .Where(x =>
                    x.ImplementsGenericInterfaceFor(
                        contextType,
                        typeof(IQueryContextSource<>)))
                .ToArray();

        var asyncSources =
            typeList
                .Where(x =>
                    x.ImplementsGenericInterfaceFor(
                        contextType,
                        typeof(IQueryContextSourceAsync<>)))
                .ToArray();

        if (syncSources.Length > 1 || asyncSources.Length > 1)
        {
            // Duplicate validation is handled by the validator — skip silently here.
            return;
        }

        if (syncSources.Length == 1 && asyncSources.Length == 1)
        {
            // Exclusivity violation — handled by the validator.
            return;
        }

        if (syncSources.Length == 1)
        {
            services.TryAddScoped(
                typeof(IQueryContextSource<>).MakeGenericType(contextType),
                syncSources[0]);
        }
        else if (asyncSources.Length == 1)
        {
            services.TryAddScoped(
                typeof(IQueryContextSourceAsync<>).MakeGenericType(contextType),
                asyncSources[0]);
        }
    }

    private static void RegisterContextEngines(
        IServiceCollection services,
        Type contextType,
        IEnumerable<Type> types)
    {
        var typeList = types as Type[] ?? types.ToArray();

        var hasLocalSource =
            typeList.Any(x =>
                x.ImplementsGenericInterfaceFor(
                    contextType,
                    typeof(IQueryContextSource<>),
                    typeof(IQueryContextSourceAsync<>)));

        var localViewTypes =
            typeList
                .Where(x =>
                    x.GetCustomAttribute<QueryViewAttribute>() is not null)
                .SelectMany(x =>
                    x.GetViewSourceInterfaces()
                        .Where(i => i.GenericTypeArguments[0] == contextType)
                        .Select(i => i.GenericTypeArguments[1]))
                .Where(x => x != contextType)
                .Distinct()
                .ToArray();

        if (hasLocalSource)
        {
            services.TryAddScoped(
                typeof(IQueryContextEngine<,>)
                    .MakeGenericType(
                        contextType,
                        contextType),
                typeof(QueryContextEngine<,>)
                    .MakeGenericType(
                        contextType,
                        contextType));

            foreach (var viewType in localViewTypes)
            {
                services.TryAddScoped(
                    typeof(IQueryContextEngine<,>)
                        .MakeGenericType(
                            contextType,
                            viewType),
                    typeof(QueryContextEngine<,>)
                        .MakeGenericType(
                            contextType,
                            viewType));
            }
        }

    }

    private static bool IsDelegatedQueryView(Type queryViewType) =>
        queryViewType.GetDelegateViewSourceInterfaces().Length > 0;

    private static void RegisterDelegatedQueryView(
        IServiceCollection services,
        Type queryViewType)
    {
        services.TryAddScoped(queryViewType);

        var interfaces =
            queryViewType.GetDelegateViewSourceInterfaces();

        foreach (var queryViewInterface in interfaces)
        {
            services.AddScoped(
                queryViewInterface,
                queryViewType);

            services.TryAddScoped(
                typeof(IDelegatedQueryViewEngine<,>)
                    .MakeGenericType(
                        queryViewInterface.GenericTypeArguments[0],
                        queryViewInterface.GenericTypeArguments[1]),
                typeof(DelegatedQueryViewEngine<,>)
                    .MakeGenericType(
                        queryViewInterface.GenericTypeArguments[0],
                        queryViewInterface.GenericTypeArguments[1]));
        }
    }

    private static void RegisterQueryView(
        IServiceCollection services,
        Type queryViewType)
    {
        var syncInterfaces =
            queryViewType.GetSyncViewSourceInterfaces();

        var asyncInterfaces =
            queryViewType.GetAsyncViewSourceInterfaces();

        if (syncInterfaces.Length == 0 && asyncInterfaces.Length == 0)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.QryInvalidRegistration,
                $"Query view '{queryViewType.FullName}' does not implement IQueryViewSource or IQueryViewSourceAsync.");
        }

        // Exclusivity is validated by QueryViewRegistrationValidator; skip registration if both are present.
        var interfaces = syncInterfaces.Length > 0 ? syncInterfaces : asyncInterfaces;

        //
        // Register the actual QueryView implementation
        //
        services.TryAddScoped(queryViewType);

        //
        // Register all implemented interfaces
        //
        foreach (var queryViewInterface in interfaces)
        {
            services.AddScoped(
                queryViewInterface,
                queryViewType);
        }
    }
}
