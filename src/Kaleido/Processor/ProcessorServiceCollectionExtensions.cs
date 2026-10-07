using System.Reflection;
using Kaleido.Processor.Context;
using Kaleido.Processor.Eventing;
using Kaleido.Processor.Observability;
using Kaleido.Processor.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Kaleido.Processor;

public static class ProcessorServiceCollectionExtensions
{
    internal static IKaleidoBuilder AddProcessor(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Assemblies.Count == 0)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.MissingAssembly,
                "At least one assembly must be registered before AddProcessor().");
        }

        var types = builder.Assemblies.ScanTypes();

        var candidateTypes =
            types
                .Where(x =>
                    IsProcessStepType(x) ||
                    x.GetCustomAttribute<ProcessStepAttribute>() is not null)
                .Where(x =>
                    x.PassesTypeFilter(
                        builder.ServiceOptions.TypeFilter,
                        ProcessorErrorCodes.InvalidRegistration,
                        "process step"))
                .ToArray();

        ValidateAttributesHaveInterface(candidateTypes);

        var recordTypes =
            candidateTypes
                .Where(IsProcessStepType)
                .ToArray();

        if (recordTypes.Length == 0)
        {
            // No process steps to register - this is valid for Queryable-only services
            return builder;
        }

        ValidateProcessSteps(recordTypes);

        var handlerTypes = new Dictionary<Type, Type>();

        foreach (var recordType in recordTypes)
        {
            var handlerType = RegisterHandler(
                builder.Services,
                recordType,
                types);

            handlerTypes[recordType] = handlerType;
        }

        builder.Services.TryAddSingleton<IProcessorStepRegistry>(
            _ => new ProcessorStepRegistry(
                recordTypes,
                handlerTypes,
                builder.ServiceOptions.DefaultAuthorization));

        builder.Services.TryAddSingleton<IProcessorRegistry>(
            sp => new ProcessorRegistry(
                sp.GetRequiredService<ITypeDescriber>(),
                sp.GetRequiredService<IConstraintMapper>(),
                builder.ServiceOptions,
                sp.GetRequiredService<IProcessorStepRegistry>(),
                sp.GetRequiredService<ILogger<ProcessorRegistry>>()));

        RegisterFrameworkServices(builder.Services);

        return builder;
    }

    private static void ValidateProcessSteps(
        IReadOnlyCollection<Type> stepTypes)
    {
        if (stepTypes.Count == 0)
        {
            // No process steps to register - this is valid for Queryable-only services
            return;
        }

        foreach (var stepType in stepTypes)
        {
            var metadata =
                GetProcessStepMetadata(stepType);

            RequireNonEmpty(stepType, metadata.Version, nameof(ProcessStepAttribute.Version));
            RequireNonEmpty(stepType, metadata.DisplayName, nameof(ProcessStepAttribute.DisplayName));
            RequireNonEmpty(stepType, metadata.Description, nameof(ProcessStepAttribute.Description));
        }

        var duplicateNames =
            stepTypes
                .Select(x => new
                {
                    StepType = x,
                    Name = x.Name
                })
                .GroupBy(
                    x => x.Name,
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

        throw new KaleidoConfigurationException(
            ProcessorErrorCodes.DuplicateStep,
            $"Duplicate process step names were found.{Environment.NewLine}{duplicateDetails}");
    }

    private static bool IsProcessStepType(
        Type type)
    {
        return type is { IsClass: true, IsAbstract: false }
            && typeof(IProcessStep).IsAssignableFrom(type);
    }

    private static void ValidateAttributesHaveInterface(
        IEnumerable<Type> types)
    {
        var invalid =
            types
                .Where(x =>
                    x.GetCustomAttribute<ProcessStepAttribute>() is not null &&
                    !typeof(IProcessStep).IsAssignableFrom(x))
                .Select(x => x.FullName)
                .ToArray();

        if (invalid.Length > 0)
        {
            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.InvalidRegistration,
                $"[ProcessStep] is applied to types that do not implement {nameof(IProcessStep)}: {string.Join(", ", invalid)}. "
                + $"A process step is identified by {nameof(IProcessStep)}; the attribute only describes it.");
        }
    }

    private static void RequireNonEmpty(
        Type stepType,
        string? value,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.MissingAttribute,
                $"Process step '{stepType.FullName}' must specify a non-empty {propertyName} on [ProcessStep].");
        }
    }

    private static ProcessStepAttribute GetProcessStepMetadata(
        Type stepType)
    {
        var metadata =
            stepType.GetCustomAttribute<ProcessStepAttribute>();

        if (metadata is null)
        {
            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.MissingAttribute,
                $"Process step '{stepType.FullName}' implements {nameof(IProcessStep)} but is missing the required [ProcessStep] attribute.");
        }

        return metadata;
    }

    private static void RegisterFrameworkServices(IServiceCollection services)
    {
        services.TryAddSingleton<IProcessorStepRegistry, ProcessorStepRegistry>();

        services.TryAddSingleton<IProcessorPlanner, ProcessorPlanner>();
        services.TryAddSingleton<IStepCandidateBuilder, StepCandidateBuilder>();
        services.TryAddSingleton<IStepCandidateConsistencyChecker, StepCandidateConsistencyChecker>();
        services.TryAddSingleton<IStepCandidatePlanner, StepCandidatePlanner>();
        services.TryAddSingleton<IStepCandidateValidator, StepCandidateValidator>();

        services.TryAddScoped<IProcessStepInvoker, ProcessorStepInvoker>();
        services.TryAddSingleton<IStepExecutionEvaluator, StepExecutionEvaluator>();
        services.TryAddSingleton<IProcessorStateUpdater, ProcessorStateUpdater>();
        services.TryAddSingleton<IStepAvailabilityResolver, StepAvailabilityResolver>();
        services.TryAddSingleton<IProcessorContextStore, ProcessorContextStore>();

        services.TryAddSingleton<IProcessorEventFactory, ProcessorEventFactory>();
        services.TryAddScoped<IProcessorObservability, ProcessorObservability>();
        services.TryAddScoped<IProcessorRuntime, ProcessorRuntime>();
        services.TryAddScoped<IProcessorExecutor, ProcessorExecutor>();
    }

    private static Type RegisterHandler(
        IServiceCollection services,
        Type stepType,
        IEnumerable<Type> types)
    {
        var handlerTypes =
            types
                .Where(type =>
                    type.ImplementsGenericInterfaceFor(
                        stepType,
                        typeof(IProcessStepHandler<>),
                        typeof(IProcessStepHandler<,>)))
                .ToArray();

        if (handlerTypes.Length == 0)
        {
            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.MissingHandler,
                $"Process step '{stepType.Name}' ({stepType.FullName}) does not have a registered handler.");
        }

        if (handlerTypes.Length > 1)
        {
            var handlers =
                string.Join(
                    ", ",
                    handlerTypes.Select(x => x.FullName));

            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.InvalidHandler,
                $"Process step '{stepType.Name}' ({stepType.FullName}) has multiple handlers: {handlers}.");
        }

        var handlerType = handlerTypes[0];
        services.AddScoped(handlerType);
        return handlerType;
    }
}
