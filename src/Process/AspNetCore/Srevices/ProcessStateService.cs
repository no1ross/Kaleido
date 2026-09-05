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
    ProcessRouteOptions routeOptions)
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

            RequiredStep =
                context.RequiredStep is null
                    ? null
                    : ProcessContractMapper.ToStepInfo(
                        context.RequiredStep,
                        registry,
                        routeOptions),

            AvailableSteps =
                context.AvailableSteps
                    .Select(reference =>
                        ProcessContractMapper.ToStepInfo(
                            reference,
                            registry,
                            routeOptions))
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
