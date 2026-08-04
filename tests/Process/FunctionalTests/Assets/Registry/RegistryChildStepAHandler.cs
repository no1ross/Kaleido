using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Assets.Registry;

public sealed class RegistryChildStepAHandler :
    IProcessStepHandler<RegistryChildStepA, RegistryChildStepAResponse>
{
    public Task<ProcessStepHandlerResult<RegistryChildStepAResponse>> ExecuteAsync(
        RegistryChildStepA step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            new ProcessStepHandlerResult<RegistryChildStepAResponse>
            {
                Response = new RegistryChildStepAResponse()
            });
    }
}
