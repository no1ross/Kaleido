using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Queryable;
using Kaleido.Queryable.Registry;
using Kaleido.Queryable.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Queryable;

public static class QueryableServiceCollectionExtensions
{
    public static IKaleidoBuilder AddQueryable(this IKaleidoBuilder builder)
    {
        return builder.AddQueryable(_ => { });
    }

    public static IKaleidoBuilder AddQueryable(
        this IKaleidoBuilder builder,
        Action<QueryableOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new QueryableOptions();
        configure(options);

        if (!builder.Assemblies.Any())
        {
            throw new InvalidOperationException(
                "At least one assembly must be registered before AddQueryable().");
        }

        var discovery = QueryableDiscovery.Scan(
            builder.Assemblies);

        if (options.ValidateRegistrations)
        {
            RegistrationValidator.Validate(
                discovery);
        }

        var registrations = discovery.Records
            .Select(x =>
                new RecordRegistration(
                    x.RecordType,
                    x.Metadata))
            .ToArray();

        RegisterFrameworkServices(
            builder.Services,
            registrations);

        RegisterSources(
            builder.Services,
            discovery);

        RegisterNamedQueries(
            builder.Services,
            discovery);

        return builder;
    }

    private static void RegisterFrameworkServices(
        IServiceCollection services,
        IReadOnlyList<RecordRegistration> registrations)
    {
        services.TryAddSingleton<IRecordMetadataCatalog, RecordMetadataCatalog>();
        services.TryAddSingleton<IRecordDescriptorFactory, RecordDescriptorFactory>();
        services.TryAddSingleton<IRecordQueryValidator, RecordQueryValidator>();
        services.TryAddSingleton<IRecordQueryCompiler, RecordQueryCompiler>();

        services.TryAddSingleton<IRecordRegistry>(
            _ => new RecordRegistry(registrations));

        services.TryAddScoped<IRecordDispatcher, RecordDispatcher>();
        services.TryAddScoped<IQueryableCatalog, QueryableCatalog>();

        services.TryAddSingleton(
            typeof(IQueryableCompiledQueryApplier<>),
            typeof(QueryableCompiledQueryApplier<>));

        services.TryAddSingleton(
            typeof(IQueryableRecordExecutor<>),
            typeof(QueryableRecordExecutor<>));
    }

    private static void RegisterSources(
        IServiceCollection services,
        RecordDiscoveryResult discovery)
    {
        foreach (var source in discovery.Sources)
        {
            services.TryAddScoped(
                source.InterfaceType,
                source.ImplementationType);

            services.TryAdd(
                ServiceDescriptor.Scoped(
                    typeof(IRecordQueryEngine<>)
                        .MakeGenericType(source.RecordType),
                    typeof(QueryableRecordQueryEngine<>)
                        .MakeGenericType(source.RecordType)));
        }
    }

    private static void RegisterNamedQueries(
        IServiceCollection services,
        RecordDiscoveryResult discovery)
    {
        foreach (var query in discovery.NamedQueries)
        {
            services.AddScoped(
                query.InterfaceType,
                query.ImplementationType);
        }
    }
}
