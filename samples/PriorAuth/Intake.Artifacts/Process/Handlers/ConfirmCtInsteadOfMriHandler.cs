using Kaleido.Process.Participant.Execution;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Handlers;

public sealed class ConfirmCtInsteadOfMriHandler
    : IProcessStepHandler<ConfirmCtInsteadOfMriStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        ConfirmCtInsteadOfMriStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            ProcessStepHandlerResult.Success());
    }
}
