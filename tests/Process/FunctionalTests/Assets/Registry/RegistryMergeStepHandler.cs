using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Assets.Registry;

public sealed class RegistryMergeStepHandler :
    IProcessStepHandler<RegistryMergeStep, RegistryMergeStepResponse>
{
    public Task<ProcessStepHandlerResult<RegistryMergeStepResponse>> ExecuteAsync(
        RegistryMergeStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            new ProcessStepHandlerResult<RegistryMergeStepResponse>
            {
                Response = new RegistryMergeStepResponse()
            });
    }
}
