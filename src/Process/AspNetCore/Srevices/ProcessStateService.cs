using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.Context;
using Kaleido.Process.Execution;
using Kaleido.Process.Registry;

namespace Kaleido.Process.AspNetCore.Srevices;

public interface IProcessStateService
{
    Task<ProcessStateResponse?> GetCurrentState(
        Guid processId,
        CancellationToken cancellationToken);
}

public class ProcessStateService(
    IProcessContextStore contextStore,
    IProcessStepRegistry registry,
    KaleidoServiceOptions serviceOptions)
    : IProcessStateService
{
    public async Task<ProcessStateResponse?> GetCurrentState(Guid processId, CancellationToken cancellationToken)
    {
        var context = await contextStore.LoadAsync(processId, cancellationToken);
        if (context == null) return null;

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
                        ProcessContractMapper.ToSummary(
                            registry.Find(stepName)
                                ?? throw new InvalidOperationException(
                                    $"Available step '{stepName}' was not found in the local registry."),
                            serviceOptions.ServiceName))
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
