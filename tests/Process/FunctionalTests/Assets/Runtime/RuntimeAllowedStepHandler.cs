using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

public sealed class RuntimeAllowedStepHandler :
    IProcessStepHandler<RuntimeAllowedStep, RuntimeAllowedStepResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeAllowedStepResponse>> ExecuteAsync(
        RuntimeAllowedStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeAllowedStepResponse>.Success(
                new RuntimeAllowedStepResponse()));
    }
}
