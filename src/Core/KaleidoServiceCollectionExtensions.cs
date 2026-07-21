using Kaleido.Metadata;
using Kaleido.Queryable;
using Kaleido.Registry;
using Kaleido.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido;

public static class KaleidoServiceCollectionExtensions
{
    public static IServiceCollection AddKaleido(
        this IServiceCollection services,
        Action<KaleidoOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KaleidoOptions();

        configure(options);

        if (options.Assemblies.Count == 0)
        {
            throw new InvalidOperationException(
                "No assemblies were registered for Kaleido scanning.");
        }

        var discovery = KaleidoDiscovery.Scan(
            options.Assemblies);

        if (options.ValidateRegistrations)
        {
            KaleidoRegistrationValidator.Validate(
                discovery);
        }

        var registrations = discovery.Records
            .Select(x =>
                new RecordRegistration(
                    x.Metadata.Name,
                    x.RecordType,
                    x.Metadata))
            .ToArray();

        RegisterFrameworkServices(
            services,
            registrations);

        RegisterSources(
            services,
            discovery);

        RegisterNamedQueries(
            services,
            discovery);

        return services;
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
            _ => new KaleidoRecordRegistry(registrations));

        services.TryAddScoped<IRecordDispatcher, RecordDispatcher>();
        services.TryAddScoped<IKaleidoCatalog, KaleidoCatalog>();

        services.TryAddSingleton(
            typeof(IQueryableCompiledQueryApplier<>),
            typeof(QueryableCompiledQueryApplier<>));

        services.TryAddSingleton(
            typeof(IQueryableRecordExecutor<>),
            typeof(QueryableRecordExecutor<>));
    }

    private static void RegisterSources(
        IServiceCollection services,
        KaleidoDiscoveryResult discovery)
    {
        foreach (var source in discovery.Sources)
        {
            services.AddScoped(
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
        KaleidoDiscoveryResult discovery)
    {
        foreach (var query in discovery.NamedQueries)
        {
            services.AddScoped(
                query.InterfaceType,
                query.ImplementationType);
        }
    }
}
