using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

public sealed class RuntimeRequiredStepHandler :
    IProcessStepHandler<RuntimeRequiredStep, RuntimeRequiredStepResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeRequiredStepResponse>> ExecuteAsync(
        RuntimeRequiredStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeRequiredStepResponse>.Success(
                new RuntimeRequiredStepResponse()));
    }
}
