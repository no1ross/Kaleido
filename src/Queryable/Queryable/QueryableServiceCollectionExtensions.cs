using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Kaleido.Queryable;

public static class QueryableServiceCollectionExtensions
{
    public static IQueryableBuilder AddQueryable(this IKaleidoBuilder builder)
    {
        return builder.AddQueryable(_ => { });
    }

    public static IQueryableBuilder AddQueryable(
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

        var recordTypes =
            types
                .Where(x =>
                    x.GetCustomAttribute<QueryableRecordAttribute>() is not null)
                .ToArray();

        foreach (var recordType in recordTypes)
        {
            RegisterRecord(
                builder.Services,
                recordType,
                types);
        }

        builder.Services.TryAddSingleton<RecordRegistrationValidator, RecordRegistrationValidator>();

        builder.Services.TryAddSingleton<IRecordRegistry>(
            sp =>
            {
                var validator =
                    sp.GetRequiredService<RecordRegistrationValidator>();

                validator.Validate(
                    recordTypes,
                    builder.Services);

                return new RecordRegistry(
                    builder.Services,
                    recordTypes);
            });

        RegisterFrameworkServices(builder.Services);

        return new QueryableBuilder(builder);
    }

    private static void RegisterFrameworkServices(IServiceCollection services)
    {
        services.TryAddSingleton<IRecordQueryValidator, QueryRequestValidator>();
        services.TryAddSingleton<IRecordQueryCompiler, QueryRequestCompiler>();
        services.TryAddSingleton<IQueryableService, QueryableService>();

        services.TryAddSingleton(typeof(ICompiledQueryApplier<>), typeof(CompiledQueryApplier<>));

        services.TryAddSingleton(typeof(IRecordExecutor<>), typeof(RecordExecutor<>));
    }

    private static void RegisterRecord(IServiceCollection services, Type recordType, IReadOnlyCollection<Type> types)
    {
        RegisterSource(services, recordType, types);

        RegisterNamedQueries(services, recordType, types);
    }

    private static void RegisterSource(IServiceCollection services, Type recordType, IEnumerable<Type> types)
    {
        var sourceType =
            types.Single(x =>
                x.GetInterfaces()
                    .Any(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() ==
                        typeof(IRecordSource<>) &&
                        i.GenericTypeArguments[0] == recordType));

        var sourceInterface =
            typeof(IRecordSource<>)
                .MakeGenericType(recordType);

        services.TryAddScoped(
            sourceInterface,
            sourceType);

        services.TryAdd(
            ServiceDescriptor.Scoped(
                typeof(IRecordQueryEngine<>)
                    .MakeGenericType(recordType),
                typeof(RecordQueryEngine<>)
                    .MakeGenericType(recordType)));
    }

    private static void RegisterNamedQueries(IServiceCollection services, Type recordType, IEnumerable<Type> types)
    {
        var queryInterface =
            typeof(IRecordNamedQuery<>)
                .MakeGenericType(recordType);

        var queries =
            types.Where(x =>
                x.GetInterfaces()
                    .Any(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() ==
                        typeof(IRecordNamedQuery<>) &&
                        i.GenericTypeArguments[0] == recordType));

        foreach (var query in queries)
        {
            services.AddScoped(queryInterface, query);
        }
    }
}
