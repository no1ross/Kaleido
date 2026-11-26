using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Observability;
using Kaleido.Queryable.Registry;
using Kaleido.Queryable.Runtime;
using Kaleido.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Queryable;

/// <summary>Registers Queryable runtime services and typed Queryable outbox event dispatch.</summary>
public static class QueryableServiceCollectionExtensions
{
    /// <summary>Registers dispatch for Queryable events on a host that relays their outbox, without registering a Queryable runtime.</summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IKaleidoBuilder AddQueryableEventDispatch(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IOutboxEventDispatcher, QueryableOutboxEventDispatcher>());
        return builder;
    }

    /// <summary>Discovers and registers query sources, views and their Queryable runtime from the builder's assemblies.</summary>
    /// <param name="builder">The Kaleido builder, with an explicit assembly scan set.</param>
    /// <param name="options">Queryable settings from code, overridden by the <c>Kaleido:Queryable</c> section.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">Assemblies, Queryable settings or discovered capabilities are invalid.</exception>
    public static IKaleidoBuilder AddQueryable(this IKaleidoBuilder builder, KaleidoQueryableOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var queryOptions = builder.BindOptions(options).Snapshot();

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

        builder.AddQueryableEventDispatch();
        builder.Services.TryAddSingleton(queryOptions);

        foreach (var type in localSourceTypes.Concat(delegatedSourceTypes).Concat(viewTypes))
        {
            builder.Services.TryAddScoped(type);
        }

        builder.Services.TryAddSingleton<QuerySourceRegistry>(
            sp => new QuerySourceRegistry(
                sp.GetRequiredService<TypeDescriber>(),
                sp.GetRequiredService<ConstraintMapper>(),
                localSourceTypes,
                delegatedSourceTypes,
                queryOptions.DefaultAuthorization ?? AuthorizationMetadata.Unspecified));

        builder.Services.TryAddSingleton<QueryViewRegistry>(
            sp => new QueryViewRegistry(
                sp.GetRequiredService<TypeDescriber>(),
                sp.GetRequiredService<ConstraintMapper>(),
                viewTypes));

        builder.Services.TryAddSingleton<IQuerySourceRegistry>(sp => sp.GetRequiredService<QuerySourceRegistry>());
        builder.Services.TryAddSingleton<IQueryViewRegistry>(sp => sp.GetRequiredService<QueryViewRegistry>());

        builder.Services.TryAddSingleton<IQueryableRegistry, QueryableRegistry>();
        builder.Services.AddSingleton<IKaleidoLocalCapabilities>(sp => (IKaleidoLocalCapabilities)sp.GetRequiredService<IQueryableRegistry>());

        RegisterFrameworkServices(builder.Services);

        return builder;
    }

    private static void RegisterFrameworkServices(IServiceCollection services)
    {
        services.TryAddSingleton<IQueryRequestValidator, QueryRequestValidator>();
        services.TryAddSingleton<IQueryRequestCompiler, QueryRequestCompiler>();
        // Scoped: it reads the request's correlation, which the query's own scope starts from (#413).
        services.TryAddScoped<IQueryableService, QueryableService>();

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
}
