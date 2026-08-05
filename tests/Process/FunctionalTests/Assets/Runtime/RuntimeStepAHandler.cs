using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

public sealed class RuntimeStepAHandler :
    IProcessStepHandler<RuntimeStepA, RuntimeStepAResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeStepAResponse>> ExecuteAsync(
        RuntimeStepA step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeStepAResponse>.Success(
                new RuntimeStepAResponse()));
    }
}
