using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

public sealed class RuntimeRootStepHandler :
    IProcessStepHandler<RuntimeRootStep, RuntimeRootStepResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeRootStepResponse>> ExecuteAsync(
        RuntimeRootStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeRootStepResponse>.Success(
                new RuntimeRootStepResponse()));
    }
}
