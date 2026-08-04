using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Assets.InvalidRegistry.DuplicateHandlers;

public sealed class DuplicateHandlerB :
    IProcessStepHandler<DuplicateHandlerStep, DuplicateHandlerStepResponse>
{
    public Task<ProcessStepHandlerResult<DuplicateHandlerStepResponse>> ExecuteAsync(
        DuplicateHandlerStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            new ProcessStepHandlerResult<DuplicateHandlerStepResponse>
            {
                Response = new DuplicateHandlerStepResponse()
            });
    }
}
