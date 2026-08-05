using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

public sealed class RuntimeMergeStepHandler :
    IProcessStepHandler<RuntimeMergeStep, RuntimeMergeStepResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeMergeStepResponse>> ExecuteAsync(
        RuntimeMergeStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeMergeStepResponse>.Success(
                new RuntimeMergeStepResponse()));
    }
}
