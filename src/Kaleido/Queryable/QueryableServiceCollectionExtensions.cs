using Kaleido.Exceptions;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Observability;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Kaleido.Queryable;

public static class QueryableServiceCollectionExtensions
{
    internal static IKaleidoBuilder AddQueryable(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Assemblies.Any())
        {
            throw new KaleidoConfigurationException(
                "At least one assembly must be registered before AddQueryable().");
        }

        var types = builder.Assemblies
            .Distinct()
            .SelectMany(x => x.DefinedTypes)
            .Where(x =>
                x.IsClass &&
                !x.IsAbstract &&
                (
                    x.IsPublic ||
                    x.IsNestedPublic ||
                    x.IsNotPublic ||
                    x.IsNestedAssembly
                ))
            .Select(x => x.AsType())
            .ToArray();

        var queryContextTypes =
            types
                .Where(x =>
                    x.GetCustomAttribute<QueryContextAttribute>() is not null)
                .Where(x =>
                    ShouldIncludeQueryableType(
                        x,
                        builder.ServiceOptions.TypeFilter))
                .ToArray();

        if (queryContextTypes.Length == 0)
        {
            // No query contexts to register - this is valid for Process-only services
            return builder;
        }

        var delegatedContextTypes =
            queryContextTypes
                .Where(x => x.GetCustomAttribute<QueryContextAttribute>()!.Kind == QueryContextKind.Delegated)
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
                    ShouldIncludeQueryableType(
                        x,
                        builder.ServiceOptions.TypeFilter))
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
                viewType,
                types);
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
                    builder.Services,
                    localQueryViewTypes);
            });

        builder.Services.TryAddSingleton<IDelegatedQueryViewRegistry>(
            _ => new DelegatedQueryViewRegistry(
                delegatedQueryViewTypes));

        builder.Services.TryAddSingleton<IQueryableRegistry, QueryableRegistry>();

        RegisterFrameworkServices(builder.Services);

        return builder;
    }

    private static bool ShouldIncludeQueryableType(
        Type queryableType,
        Func<Type, bool>? typeFilter)
    {
        try
        {
            return typeFilter?.Invoke(queryableType) ?? true;
        }
        catch (Exception exception)
        {
            throw new KaleidoConfigurationException(
                $"Type filter failed for type '{queryableType.FullName}'.",
                exception);
        }
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
                    x.GetInterfaces()
                        .Any(i =>
                            i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IQueryContextSource<>) &&
                            i.GenericTypeArguments[0] == contextType))
                .ToArray();

        var asyncSources =
            typeList
                .Where(x =>
                    x.GetInterfaces()
                        .Any(i =>
                            i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IQueryContextSourceAsync<>) &&
                            i.GenericTypeArguments[0] == contextType))
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
                x.GetInterfaces()
                    .Any(i =>
                        i.IsGenericType &&
                        (
                            i.GetGenericTypeDefinition() == typeof(IQueryContextSource<>) ||
                            i.GetGenericTypeDefinition() == typeof(IQueryContextSourceAsync<>)
                        ) &&
                        i.GenericTypeArguments[0] == contextType));

        var localViewTypes =
            typeList
                .Where(x =>
                    x.GetCustomAttribute<QueryViewAttribute>() is not null)
                .SelectMany(x =>
                    x.GetInterfaces()
                        .Where(i =>
                            i.IsGenericType &&
                            (
                                i.GetGenericTypeDefinition() == typeof(IQueryViewSource<,>) ||
                                i.GetGenericTypeDefinition() == typeof(IQueryViewSource<,,>) ||
                                i.GetGenericTypeDefinition() == typeof(IQueryViewSourceAsync<,>) ||
                                i.GetGenericTypeDefinition() == typeof(IQueryViewSourceAsync<,,>)
                            ) &&
                            i.GenericTypeArguments[0] == contextType)
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
        queryViewType
            .GetInterfaces()
            .Any(i =>
                i.IsGenericType &&
                (
                    i.GetGenericTypeDefinition() == typeof(IDelegateQueryViewSource<,>) ||
                    i.GetGenericTypeDefinition() == typeof(IDelegateQueryViewSource<,,>)
                ));

    private static void RegisterDelegatedQueryView(
        IServiceCollection services,
        Type queryViewType)
    {
        services.TryAddScoped(queryViewType);

        var interfaces =
            queryViewType
                .GetInterfaces()
                .Where(i =>
                    i.IsGenericType &&
                    (
                        i.GetGenericTypeDefinition() == typeof(IDelegateQueryViewSource<,>) ||
                        i.GetGenericTypeDefinition() == typeof(IDelegateQueryViewSource<,,>)
                    ))
                .ToArray();

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
        Type queryViewType,
        IEnumerable<Type> types)
    {
        var syncInterfaces =
            queryViewType
                .GetInterfaces()
                .Where(i =>
                    i.IsGenericType &&
                    (
                        i.GetGenericTypeDefinition() == typeof(IQueryViewSource<,>) ||
                        i.GetGenericTypeDefinition() == typeof(IQueryViewSource<,,>)
                    ))
                .ToArray();

        var asyncInterfaces =
            queryViewType
                .GetInterfaces()
                .Where(i =>
                    i.IsGenericType &&
                    (
                        i.GetGenericTypeDefinition() == typeof(IQueryViewSourceAsync<,>) ||
                        i.GetGenericTypeDefinition() == typeof(IQueryViewSourceAsync<,,>)
                    ))
                .ToArray();

        if (syncInterfaces.Length == 0 && asyncInterfaces.Length == 0)
        {
            throw new KaleidoConfigurationException(
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