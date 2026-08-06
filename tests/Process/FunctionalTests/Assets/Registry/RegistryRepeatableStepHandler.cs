using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Assets.Registry;

public sealed class RegistryRepeatableStepHandler
    : IProcessStepHandler<
        RegistryRepeatableStep,
        RegistryRepeatableStepResponse>
{
    public Task<ProcessStepHandlerResult<RegistryRepeatableStepResponse>> ExecuteAsync(
        RegistryRepeatableStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RegistryRepeatableStepResponse>.Success(
                new RegistryRepeatableStepResponse()));
    }
}