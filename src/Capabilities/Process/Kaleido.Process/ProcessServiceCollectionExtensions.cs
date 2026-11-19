using Kaleido.Process.Context;
using Kaleido.Process.Eventing;
using Kaleido.Process.Observability;
using Kaleido.Process.Recovery;
using Kaleido.Process.Registry;
using Kaleido.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Kaleido.Process;

/// <summary>Registers Process runtime services and typed Process outbox event dispatch.</summary>
public static class ProcessServiceCollectionExtensions
{
    /// <summary>Registers dispatch for Process events on a host that relays their outbox, without registering a Process runtime.</summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IKaleidoBuilder AddProcessEventDispatch(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IOutboxEventDispatcher, ProcessOutboxEventDispatcher>());
        return builder;
    }

    /// <summary>Discovers and registers Process steps, handlers and the in-process execution runtime.</summary>
    /// <param name="builder">The Kaleido builder, with an explicit assembly scan set.</param>
    /// <param name="options">Process settings from code, overridden by the <c>Kaleido:Process</c> section.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">Assemblies, Process settings or step registrations are invalid.</exception>
    public static IKaleidoBuilder AddProcess(this IKaleidoBuilder builder, KaleidoProcessOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var processOptions = builder.BindOptions(options).Snapshot();

        if (builder.Assemblies.Count == 0)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.MissingAssembly,
                "At least one assembly must be registered before AddProcess().");
        }

        var types = builder.Assemblies.ScanTypes();
        var (recordTypes, handlerTypes) =
            types.DiscoverProcessCapabilities(builder.ServiceOptions.TypeFilter);

        if (recordTypes.Length == 0)
        {
            // No process steps to register - this is valid for Queryable-only services
            return builder;
        }

        builder.Services.TryAddSingleton(processOptions);
        builder.Services.TryAddSingleton<IProcessTransactions, NoProcessTransactions>();
        builder.AddProcessEventDispatch();

        foreach (var handlerType in handlerTypes.Values)
        {
            builder.Services.AddScoped(handlerType);
        }

        RegisterRegistries(builder, recordTypes, handlerTypes, processOptions);
        RegisterFrameworkServices(builder.Services);

        return builder;
    }

    /// <summary>Explicitly registers a scoped process-state store; a different provider choice conflicts.</summary>
    /// <typeparam name="TStore">The process-state store implementation.</typeparam>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">Another context store has already been chosen.</exception>
    public static IKaleidoBuilder AddContextStore<TStore>(this IKaleidoBuilder builder)
        where TStore : class, IProcessContextStore =>
        KaleidoServiceCollectionExtensions.Replace<IProcessContextStore, TStore, TStore>(builder, "context store");

    /// <summary>Registers the scoped transaction used to save Process state and its event atomically on one database.</summary>
    /// <typeparam name="TTransactions">The persistence-specific transaction implementation.</typeparam>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">Another transaction implementation has already been chosen.</exception>
    public static IKaleidoBuilder AddTransactions<TTransactions>(this IKaleidoBuilder builder)
        where TTransactions : class, IProcessTransactions =>
        KaleidoServiceCollectionExtensions.Replace<IProcessTransactions, TTransactions, NoProcessTransactions>(builder, "transactions");

    private static void RegisterRegistries(
        IKaleidoBuilder builder,
        Type[] recordTypes,
        Dictionary<Type, Type> handlerTypes,
        KaleidoProcessOptions processOptions)
    {
        builder.Services.TryAddSingleton<IProcessorStepRegistry>(
            _ => new ProcessorStepRegistry(
                recordTypes,
                handlerTypes,
                processOptions.DefaultAuthorization ?? AuthorizationMetadata.Unspecified));

        builder.Services.TryAddSingleton<IProcessorRegistry>(
            sp => new ProcessorRegistry(
                sp.GetRequiredService<TypeDescriber>(),
                sp.GetRequiredService<ConstraintMapper>(),
                builder.ServiceOptions,
                processOptions,
                sp.GetRequiredService<IProcessorStepRegistry>(),
                sp.GetRequiredService<ILogger<ProcessorRegistry>>()));
        builder.Services.AddSingleton<IKaleidoLocalCapabilities>(sp => (IKaleidoLocalCapabilities)sp.GetRequiredService<IProcessorRegistry>());
    }

    private static void RegisterFrameworkServices(IServiceCollection services)
    {
        services.TryAddSingleton<IProcessorPlanner, ProcessorPlanner>();
        services.TryAddSingleton<IStepCandidateBuilder, StepCandidateBuilder>();
        services.TryAddSingleton<IStepCandidateConsistencyChecker, StepCandidateConsistencyChecker>();
        services.TryAddSingleton<IStepCandidatePlanner, StepCandidatePlanner>();
        services.TryAddSingleton<IStepCandidateValidator, StepCandidateValidator>();
        services.TryAddSingleton<IStepCandidateNextStepChecker, StepCandidateNextStepChecker>();
        services.TryAddSingleton<IInformationValidator, InformationValidator>();

        services.TryAddScoped<IProcessStepInvoker, ProcessorStepInvoker>();
        services.TryAddSingleton<IStepExecutionEvaluator, StepExecutionEvaluator>();
        services.TryAddSingleton<IProcessorStateUpdater, ProcessorStateUpdater>();
        services.TryAddSingleton<IStepAvailabilityResolver, StepAvailabilityResolver>();

        services.TryAddSingleton<IProcessorEventFactory, ProcessorEventFactory>();
        services.TryAddScoped<IProcessorObservability, ProcessorObservability>();
        services.TryAddScoped<IProcessorRuntime, ProcessorRuntime>();
        services.TryAddScoped<IProcessorExecutor, ProcessorExecutor>();
        services.TryAddScoped<IProcessorTransition, ProcessorTransition>();

        // Step outcomes whose save failed after the handler ran, and their recovery (#537).
        services.TryAddSingleton<IProcessorRecoveryStore, ProcessorRecoveryStore>();
        services.TryAddSingleton<IProcessorOutcomeRecovery, ProcessorOutcomeRecovery>();
        services.AddHostedService<ProcessorRecoveryService>();
    }
}
