using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Assets.Registry;

public sealed class RegistryChildStepBHandler :
    IProcessStepHandler<RegistryChildStepB, RegistryChildStepBResponse>
{
    public Task<ProcessStepHandlerResult<RegistryChildStepBResponse>> ExecuteAsync(
        RegistryChildStepB step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            new ProcessStepHandlerResult<RegistryChildStepBResponse>
            {
                Response = new RegistryChildStepBResponse()
            });
    }
}
