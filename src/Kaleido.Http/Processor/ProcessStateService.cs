using Kaleido.Processor.Context;
using Kaleido.Storage;
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
    IKaleidoContextStore contextStore,
    IProcessorStepRegistry registry,
    KaleidoServiceOptions serviceOptions,
    IProcessorResponseFactory responseFactory,
    IProcessContinuationFilter continuations,
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
        // endpoint may read the state; CreatedBy/UpdatedBy are returned as tags only.
        LogStateLoaded(logger, serviceOptions.ServiceName, processId, context.State);

        // The caller is described only the steps they may run (#524). RequiredStep is null when
        // TargetProcessorName is set: the consumer calls the target processor's state endpoint instead.
        var continuation = await continuations.FilterAsync(
            context.ProcessId,
            CreateRequiredStep(context),
            [.. context.AvailableSteps.Select(AvailableStep)],
            cancellationToken);

        return new ProcessStateResponse
        {
            ProcessId = context.ProcessId,
            State = context.State,
            RequiredStep = continuation.RequiredStep,
            TargetProcessorName = context.TargetProcessorName,
            AvailableSteps = continuation.AvailableSteps,
            Notice = continuation.Notice,
            Steps =
            [
                .. context.Steps
                    .OrderBy(x => x.StepName, StringComparer.OrdinalIgnoreCase)
                    .Select(x =>
                        new ProcessStepHistory
                        {
                            StepName = x.StepName,
                            Status = x.Status,
                            FirstExecuted = x.FirstExecuted,
                            StartedBy = x.StartedBy,
                            LastExecuted = x.LastExecuted,
                            LastUpdatedBy = x.LastUpdatedBy,
                        }),
            ],
            CreatedUtc = context.CreatedUtc,
            UpdatedUtc = context.UpdatedUtc,
            CreatedBy = context.CreatedBy,
            UpdatedBy = context.UpdatedBy,
        };
    }

    // An available step as the caller sees it; every available step is registered locally.
    private ProcessStepSummary AvailableStep(string stepName)
    {
        var registration = registry.Find(stepName)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                $"Available step '{stepName}' was not found in the local registry.");

        return responseFactory.CreateStepSummary(
            registration.ToSummary(),
            serviceOptions.ServiceName);
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
            registration.ToSummary(),
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
