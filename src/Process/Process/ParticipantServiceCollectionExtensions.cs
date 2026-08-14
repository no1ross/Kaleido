using Kaleido.Process.Attributes;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;
using Kaleido.Process.Participant.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Kaleido.Process;

public static class ParticipantServiceCollectionExtensions
{
    public static IParticipantBuilder AddParticipant(this IKaleidoBuilder builder)
    {
        return builder.AddParticipant(_ => { });
    }

    public static IParticipantBuilder AddParticipant(
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
                .Where(x =>
                    ShouldIncludeProcessStep(
                        x,
                        options))
                .ToArray();

        ValidateProcessSteps(recordTypes);

        foreach (var recordType in recordTypes)
        {
            RegisterProcessStep(
                builder.Services,
                recordType,
                types);
        }

        builder.Services.TryAddSingleton<IProcessStepRegistry>(
            sp =>
            {
                return new ProcessStepRegistry(
                    builder.Services,
                    recordTypes);
            });

        RegisterFrameworkServices(builder.Services);

        return new ParticipantBuilder(builder);
    }

    private static bool ShouldIncludeProcessStep(
        Type stepType,
        ParticipantOptions options)
    {
        try
        {
            return options.TypeFilter?.Invoke(stepType) ?? true;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"The configured TypeFilter failed while evaluating process step '{stepType.FullName ?? stepType.Name}'.",
                exception);
        }
    }

    private static void ValidateProcessSteps(
        IReadOnlyCollection<Type> stepTypes)
    {
        if (stepTypes.Count == 0)
        {
            throw new InvalidOperationException(
                "No process steps were discovered for the participant.");
        }

        foreach (var stepType in stepTypes)
        {
            var metadata =
                GetProcessStepMetadata(stepType);

            if (string.IsNullOrWhiteSpace(metadata.Name))
            {
                throw new InvalidOperationException(
                    $"Process step '{stepType.FullName}' must specify a non-empty name.");
            }

            if (string.IsNullOrWhiteSpace(metadata.Version))
            {
                throw new InvalidOperationException(
                    $"Process step '{stepType.FullName}' must specify a non-empty version.");
            }
        }

        var duplicateNames =
            stepTypes
                .Select(x => new
                {
                    StepType = x,
                    Metadata = GetProcessStepMetadata(x)
                })
                .GroupBy(
                    x => x.Metadata.Name,
                    StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .ToArray();

        if (duplicateNames.Length == 0)
        {
            return;
        }

        var duplicateDetails =
            string.Join(
                Environment.NewLine,
                duplicateNames.Select(x =>
                {
                    var stepTypesForName =
                        string.Join(
                            ", ",
                            x.Select(y => y.StepType.FullName));

                    return $"Name '{x.Key}' is used by: {stepTypesForName}";
                }));

        throw new InvalidOperationException(
            $"Duplicate process step names were found.{Environment.NewLine}{duplicateDetails}");
    }

    private static ProcessStepAttribute GetProcessStepMetadata(
        Type stepType)
    {
        var metadata =
            stepType.GetCustomAttribute<ProcessStepAttribute>();

        if (metadata is null)
        {
            throw new InvalidOperationException(
                $"Type '{stepType.FullName}' is not decorated with ProcessStepAttribute.");
        }

        return metadata;
    }

    private static void RegisterFrameworkServices(IServiceCollection services)
    {
        services.TryAddSingleton<IProcessStepRegistry, ProcessStepRegistry>();

        services.TryAddSingleton<IExecutionPlanner, ExecutionPlanner>();
        services.TryAddSingleton<IStepCandidateBuilder, StepCandidateBuilder>();
        services.TryAddSingleton<IStepCandidateConsistencyChecker, StepCandidateConsistencyChecker>();
        services.TryAddSingleton<IStepCandidatePlanner, StepCandidatePlanner>();
        services.TryAddSingleton<IStepCandidateValidator, StepCandidateValidator>();

        services.TryAddSingleton<IProcessStepInvoker, ProcessStepInvoker>();
        services.TryAddSingleton<IStepExecutionEvaluator, StepExecutionEvaluator>();
        services.TryAddSingleton<IProcessStateUpdater, ProcessStateUpdater>();
        services.TryAddSingleton<IStepAvailabilityResolver, StepAvailabilityResolver>();
        services.TryAddSingleton<IProcessContextStore, InMemoryProcessContextStore>();

        services.TryAddScoped<IParticipantRuntime, ParticipantRuntime>();
        services.TryAddScoped<IExecutionProcessor, ExecutionProcessor>();
    }

    private static void RegisterProcessStep(
        IServiceCollection services,
        Type stepType,
        IReadOnlyCollection<Type> types)
    {
        RegisterHandler(
            services,
            stepType,
            types);
    }

    private static void RegisterHandler(
        IServiceCollection services,
        Type stepType,
        IEnumerable<Type> types)
    {
        var metadata =
            GetProcessStepMetadata(stepType);

        var handlerTypes =
            types
                .Where(type =>
                    type.GetInterfaces()
                        .Any(i => IsProcessStepHandler(i, stepType)))
                .ToArray();

        if (handlerTypes.Length == 0)
        {
            throw new InvalidOperationException(
                $"Process step '{metadata.Name}' ({stepType.FullName}) does not have a registered handler.");
        }

        if (handlerTypes.Length > 1)
        {
            var handlers =
                string.Join(
                    ", ",
                    handlerTypes.Select(x => x.FullName));

            throw new InvalidOperationException(
                $"Process step '{metadata.Name}' ({stepType.FullName}) has multiple handlers: {handlers}.");
        }

        services.AddScoped(handlerTypes[0]);
    }

    private static bool IsProcessStepHandler(
        Type interfaceType,
        Type stepType)
    {
        if (!interfaceType.IsGenericType)
        {
            return false;
        }

        var definition =
            interfaceType.GetGenericTypeDefinition();

        return
            (definition == typeof(IProcessStepHandler<>) ||
             definition == typeof(IProcessStepHandler<,>))
            &&
            interfaceType.GetGenericArguments()[0] == stepType;
    }
}