using Kaleido.Processor.Context;
using Kaleido.Processor.Registry;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Processor;

internal interface IProcessStateService
{
    Task<ProcessStateResponse?> GetCurrentState(
        Guid processId,
        CancellationToken cancellationToken);
}

internal sealed partial class ProcessStateService(
    IProcessorContextStore contextStore,
    IProcessorStepRegistry registry,
    KaleidoServiceOptions serviceOptions,
    IProcessorResponseFactory responseFactory,
    ILogger<ProcessStateService> logger)
    : IProcessStateService
{
    public async Task<ProcessStateResponse?> GetCurrentState(Guid processId, CancellationToken cancellationToken)
    {
        LogLoadingState(logger, serviceOptions.ServiceName, processId);

        var context = await contextStore.LoadAsync(processId, cancellationToken);

        if (context == null)
        {
            LogStateNotFound(logger, serviceOptions.ServiceName, processId);

            return null;
        }

        // No case-level guardrail (ADR 0010): any caller admitted by the
        // endpoint may read the state; Owner is returned as a tag only.
        LogStateLoaded(logger, serviceOptions.ServiceName, processId, context.State);

        return new ProcessStateResponse
        {
            ProcessId = context.ProcessId,

            State = context.State,

            // RequiredStep is null when TargetProcessorName is set —
            // consumer must call the target processor's state endpoint instead.
            RequiredStep =
                CreateRequiredStep(
                    context),

            TargetProcessorName =
                context.TargetProcessorName,

            AvailableSteps =
                context.AvailableSteps
                    .Select(stepName =>
                        {
                            var registration = registry.Find(stepName)
                                ?? throw new KaleidoFrameworkException(
                                    FrameworkErrorCodes.MissingRegistration,
                                    $"Available step '{stepName}' was not found in the local registry.");
                            return responseFactory.CreateStepSummary(
                                new ProcessorStepSummary
                                {
                                    Name = registration.Metadata.Name,
                                    Description = registration.Metadata.Description,
                                    DisplayName = registration.Metadata.DisplayName,
                                    Version = registration.Metadata.Version,
                                    Repeatable = registration.Repeatable.Enabled,
                                    IsInformationStep = registration.IsInformationStep,
                                },
                                serviceOptions.ServiceName);
                        })
                    .ToArray(),

            Steps =
                context.Steps
                    .OrderBy(x => x.StepName, StringComparer.OrdinalIgnoreCase)
                    .Select(x =>
                        new ProcessStepHistory
                        {
                            StepName = x.StepName,
                            Version = x.Version,
                            Status = x.Status,
                            LastExecuted = x.LastExecuted,
                        })
                    .ToArray(),

            CreatedUtc = context.CreatedUtc,

            UpdatedUtc = context.UpdatedUtc,

            Owner = context.Owner,
        };
    }

    // Never builds an information request: it returns the pending one stored with the state.
    private ProcessRequiredStep? CreateRequiredStep(
        ProcessorContext context)
    {
        if (context.TargetProcessorName is not null ||
            context.RequiredStep is not { } requiredStep)
        {
            return null;
        }

        var registration = registry.Find(requiredStep)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                $"Required step '{requiredStep}' was not found in the local registry.");

        return responseFactory.CreateRequiredStep(
            new ProcessorStepSummary
            {
                Name = registration.Metadata.Name,
                Description = registration.Metadata.Description,
                DisplayName = registration.Metadata.DisplayName,
                Version = registration.Metadata.Version,
                Repeatable = registration.Repeatable.Enabled,
                IsInformationStep = registration.IsInformationStep,
            },
            context.RequiredInformationRequest,
            serviceOptions.ServiceName);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading process state for processor {ProcessorName} process {ProcessId}.")]
    private static partial void LogLoadingState(ILogger logger, string processorName, Guid processId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Process state not found for processor {ProcessorName} process {ProcessId}.")]
    private static partial void LogStateNotFound(ILogger logger, string processorName, Guid processId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Process state loaded for processor {ProcessorName} process {ProcessId} state {State}.")]
    private static partial void LogStateLoaded(ILogger logger, string processorName, Guid processId, Kaleido.Processor.ProcessExecutionState state);
}
