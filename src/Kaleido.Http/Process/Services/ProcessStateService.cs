using Kaleido.Process.Context;
using Kaleido.Process.Registry;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Process.Services;

internal interface IProcessStateService
{
    Task<ProcessStateResponse?> GetCurrentState(
        Guid processId,
        CancellationToken cancellationToken);
}

internal sealed class ProcessStateService(
    IProcessContextStore contextStore,
    IProcessStepRegistry registry,
    KaleidoServiceOptions serviceOptions,
    ILogger<ProcessStateService> logger)
    : IProcessStateService
{
    public async Task<ProcessStateResponse?> GetCurrentState(Guid processId, CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Loading process state for processor {ProcessorName} process {ProcessId}.",
            serviceOptions.ServiceName,
            processId);

        var context = await contextStore.LoadAsync(processId, cancellationToken);

        if (context == null)
        {
            logger.LogDebug(
                "Process state not found for processor {ProcessorName} process {ProcessId}.",
                serviceOptions.ServiceName,
                processId);

            return null;
        }

        logger.LogDebug(
            "Process state loaded for processor {ProcessorName} process {ProcessId} state {State}.",
            serviceOptions.ServiceName,
            processId,
            context.State);

        return new ProcessStateResponse
        {
            ProcessId = context.ProcessId,

            State = context.State,

            // RequiredStep is null when TargetProcessorName is set —
            // consumer must call the target processor's state endpoint instead.
            RequiredStep =
                context.TargetProcessorName is null
                    ? context.RequiredStep
                    : null,

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
                            return ProcessContractMapper.ToSummary(
                                ProcessRegistryProjection.ProjectSummary(registration),
                                serviceOptions.ServiceName);
                        })
                    .ToArray(),

            Steps =
                context.Steps
                    .OrderBy(x => x.StepName)
                    .Select(x =>
                        new ProcessStepHistory
                        {
                            StepName = x.StepName,
                            Version = x.Version,
                            Status = x.Status,
                            LastExecuted = x.LastExecuted
                        })
                    .ToArray(),

            CreatedUtc = context.CreatedUtc,

            UpdatedUtc = context.UpdatedUtc,
        };
    }
}
