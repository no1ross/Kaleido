using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Assets.Registry;

public sealed class RegistryRootStepHandler :
    IProcessStepHandler<RegistryRootStep, RegistryRootStepResponse>
{
    public Task<ProcessStepHandlerResult<RegistryRootStepResponse>> ExecuteAsync(
        RegistryRootStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            new ProcessStepHandlerResult<RegistryRootStepResponse>
            {
                Response = new RegistryRootStepResponse()
            });
    }
}
