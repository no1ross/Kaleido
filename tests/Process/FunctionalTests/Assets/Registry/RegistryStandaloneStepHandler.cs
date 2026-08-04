using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Assets.Registry;

public sealed class RegistryStandaloneStepHandler :
    IProcessStepHandler<RegistryStandaloneStep, RegistryStandaloneStepResponse>
{
    public Task<ProcessStepHandlerResult<RegistryStandaloneStepResponse>> ExecuteAsync(
        RegistryStandaloneStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            new ProcessStepHandlerResult<RegistryStandaloneStepResponse>
            {
                Response = new RegistryStandaloneStepResponse()
            });
    }
}
