using Kaleido.Process.Attributes;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Steps;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Kaleido.Process;


public static class ProcessServiceCollectionExtensions
{
    public static IKaleidoBuilder AddParticipant(this IKaleidoBuilder builder)
    {
        return builder.AddParticipant(_ => { });
    }

    public static IKaleidoBuilder AddParticipant(
        this IKaleidoBuilder builder,
        Action<ParticipantOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ParticipantOptions();
        configure(options);

        if (!builder.Assemblies.Any())
        {
            throw new InvalidOperationException(
                "At least one assembly must be registered before AddParticipant().");
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
                    x.GetCustomAttribute<ProcessStepAttribute>() is not null)
                .ToArray();

        foreach (var recordType in recordTypes)
        {
            RegisterProcessStep(
                builder.Services,
                recordType,
                types);
        }

        //builder.Services.TryAddSingleton<RecordRegistrationValidator, RecordRegistrationValidator>();

        builder.Services.TryAddSingleton<IProcessStepRegistry>(
            sp =>
            {
                //var validator =
                //    sp.GetRequiredService<RecordRegistrationValidator>();

                //validator.Validate(
                //    recordTypes,
                //    builder.Services);

                return new ProcessStepRegistry(
                    builder.Services,
                    recordTypes);
            });

        RegisterFrameworkServices(builder.Services);

        return builder;
    }

    private static void RegisterFrameworkServices(IServiceCollection services)
    {
        //services.TryAddSingleton<IRecordQueryValidator, QueryRequestValidator>();
        //services.TryAddSingleton<IRecordQueryCompiler, QueryRequestCompiler>();
        //services.TryAddSingleton<IQueryableService, QueryableService>();

        //services.TryAddSingleton(typeof(ICompiledQueryApplier<>), typeof(CompiledQueryApplier<>));

        //services.TryAddSingleton(typeof(IRecordExecutor<>), typeof(RecordExecutor<>));
    }

    private static void RegisterProcessStep(IServiceCollection services, Type stepType, IReadOnlyCollection<Type> types)
    {
        RegisterHandler(services, stepType, types);
    }

    private static void RegisterHandler(IServiceCollection services, Type stepType, IEnumerable<Type> types)
    {
        var handlerInterface =
            typeof(IProcessStepHandler<>)
                .MakeGenericType(stepType);

        var handlerType =
            types.Single(x =>
                x.GetInterfaces()
                    .Any(i =>
                        i == handlerInterface));

        services.AddScoped(
            handlerInterface,
            handlerType);
    }
}
