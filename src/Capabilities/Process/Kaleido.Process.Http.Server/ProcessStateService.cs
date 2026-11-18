using Microsoft.Extensions.Logging;

namespace Kaleido.Process.Http.Server;

internal interface IProcessStateService
{
    Task<ProcessStateSnapshot?> GetCurrentState(Guid processId, CancellationToken cancellationToken);
}

internal sealed partial class ProcessStateService(
    IProcessContextStore contextStore,
    KaleidoServiceOptions serviceOptions,
    IProcessContinuationFilter continuations,
    ILogger<ProcessStateService> logger)
    : IProcessStateService
{
    public async Task<ProcessStateSnapshot?> GetCurrentState(Guid processId, CancellationToken cancellationToken)
    {
        LogLoadingState(logger, serviceOptions.ServiceName, processId);
        var context = await contextStore.LoadAsync(new ProcessKey(serviceOptions.ServiceName, serviceOptions.ServiceVersion, processId), cancellationToken);
        if (context is null)
        {
            LogStateNotFound(logger, serviceOptions.ServiceName, processId);
            return null;
        }

        // No case-level guardrail (ADR 0010): any caller admitted by the
        // endpoint may read the state; CreatedBy/UpdatedBy are returned as tags only.
        LogStateLoaded(logger, serviceOptions.ServiceName, processId, context.State);

        // The caller is described only the steps they may run (#524). RequiredStep is null when
        // TargetProcessorName is set: the consumer calls the target processor's state endpoint instead.
        var snapshot = ProcessStateSnapshot.FromContext(context);
        var requiredStep = snapshot.TargetProcessorName is null ? snapshot.RequiredStep : null;
        var continuation = await continuations.FilterAsync(processId, requiredStep,
            snapshot.AvailableSteps, snapshot.TargetProcessorName, cancellationToken);
        return snapshot with
        {
            RequiredStep = continuation.RequiredStep,
            RequiredInformationRequest = continuation.RequiredStep is null ? null : snapshot.RequiredInformationRequest,
            AvailableSteps = continuation.AvailableSteps,
            Notice = continuation.Notice,
        };
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading process state for processor {ProcessorName} process {ProcessId}.")]
    private static partial void LogLoadingState(ILogger logger, string processorName, Guid processId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Process state not found for processor {ProcessorName} process {ProcessId}.")]
    private static partial void LogStateNotFound(ILogger logger, string processorName, Guid processId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Process state loaded for processor {ProcessorName} process {ProcessId} state {State}.")]
    private static partial void LogStateLoaded(ILogger logger, string processorName, Guid processId, ProcessExecutionState state);
}
