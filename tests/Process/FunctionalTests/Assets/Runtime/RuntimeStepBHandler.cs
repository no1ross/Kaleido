using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

public sealed class RuntimeStepBHandler :
    IProcessStepHandler<RuntimeStepB, RuntimeStepBResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeStepBResponse>> ExecuteAsync(
        RuntimeStepB step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeStepBResponse>.Success(
                new RuntimeStepBResponse()));
    }
}
