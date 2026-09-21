using Kaleido;
using Kaleido.Process.Attributes;
using Kaleido.Process.Context;
using Kaleido.Process.Eventing;
using Kaleido.Process.Execution;
using Kaleido.Process.Observability;
using Kaleido.Process.Planning;
using Kaleido.Process.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Kaleido.Process;

public static class ProcessServiceCollectionExtensions
{
    internal static IKaleidoBuilder AddProcessor(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Assemblies.Any())
        {
            throw new KaleidoConfigurationException(
                "At least one assembly must be registered before AddProcessor().");
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
                        builder.ServiceOptions.TypeFilter))
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

        builder.Services.TryAddSingleton<IProcessStepRegistry>(
            _ => new ProcessStepRegistry(
                recordTypes,
                handlerTypes));

        builder.Services.TryAddSingleton<IProcessRegistry>(
            sp => new ProcessRegistry(
                builder.ServiceOptions,
                sp.GetRequiredService<IProcessStepRegistry>()));

        RegisterFrameworkServices(builder.Services);

        return builder;
    }

    private static bool ShouldIncludeProcessStep(
        Type stepType,
        Func<Type, bool>? typeFilter)
    {
        try
        {
            return typeFilter?.Invoke(stepType) ?? true;
        }
        catch (Exception exception)
        {
            throw new KaleidoConfigurationException(
                $"The configured TypeFilter failed while evaluating process step '{stepType.FullName ?? stepType.Name}'. " +
                $"Error code: {ProcessErrorCodes.TypeFilterFailed}.",
                exception);
        }
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

            if (string.IsNullOrWhiteSpace(metadata.Name))
            {
                throw new KaleidoConfigurationException(
                    $"Process step '{stepType.FullName}' must specify a non-empty name. " +
                    $"Error code: {ProcessErrorCodes.InvalidStepName}.");
            }

            if (string.IsNullOrWhiteSpace(metadata.Version))
            {
                throw new KaleidoConfigurationException(
                    $"Process step '{stepType.FullName}' must specify a non-empty version. " +
                    $"Error code: {ProcessErrorCodes.InvalidStepVersion}.");
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

        throw new KaleidoConfigurationException(
            $"Duplicate process step names were found.{Environment.NewLine}{duplicateDetails} " +
            $"Error code: {ProcessErrorCodes.DuplicateStepName}.");
    }

    private static ProcessStepAttribute GetProcessStepMetadata(
        Type stepType)
    {
        var metadata =
            stepType.GetCustomAttribute<ProcessStepAttribute>();

        if (metadata is null)
        {
            throw new KaleidoConfigurationException(
                $"Type '{stepType.FullName}' is not decorated with ProcessStepAttribute. " +
                $"Error code: {ProcessErrorCodes.MissingStepAttribute}.");
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

        services.TryAddScoped<IProcessStepInvoker, ProcessStepInvoker>();
        services.TryAddSingleton<IStepExecutionEvaluator, StepExecutionEvaluator>();
        services.TryAddSingleton<IProcessStateUpdater, ProcessStateUpdater>();
        services.TryAddSingleton<IStepAvailabilityResolver, StepAvailabilityResolver>();
        services.TryAddSingleton<IProcessContextStore, InMemoryProcessContextStore>();

        services.TryAddSingleton<IProcessEventFactory, ProcessEventFactory>();
        services.TryAddScoped<IProcessObservability, ProcessObservability>();
        services.TryAddScoped<IProcessRuntime, ProcessRuntime>();
        services.TryAddScoped<IExecutionProcessor, ExecutionProcessor>();
    }

    private static Type RegisterHandler(
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
            throw new KaleidoConfigurationException(
                $"Process step '{metadata.Name}' ({stepType.FullName}) does not have a registered handler. " +
                $"Error code: {ProcessErrorCodes.MissingStepHandler}.");
        }

        if (handlerTypes.Length > 1)
        {
            var handlers =
                string.Join(
                    ", ",
                    handlerTypes.Select(x => x.FullName));

            throw new KaleidoConfigurationException(
                $"Process step '{metadata.Name}' ({stepType.FullName}) has multiple handlers: {handlers}. " +
                $"Error code: {ProcessErrorCodes.MultipleStepHandlers}.");
        }

        var handlerType = handlerTypes[0];
        services.AddScoped(handlerType);
        return handlerType;
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

        var genericArguments = interfaceType.GetGenericArguments();

        return
            (definition == typeof(IProcessStepHandler<>) ||
             definition == typeof(IProcessStepHandler<,>))
            &&
            genericArguments.Length > 0
            &&
            genericArguments[0] == stepType;
    }
}