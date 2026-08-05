using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

public sealed class RuntimeRequiredRootStepHandler :
    IProcessStepHandler<RuntimeRequiredRootStep, RuntimeRequiredRootStepResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeRequiredRootStepResponse>> ExecuteAsync(
        RuntimeRequiredRootStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeRequiredRootStepResponse>.Success(
                new RuntimeRequiredRootStepResponse(),
                RuntimeStepNames.RequiredStep));
    }
}
