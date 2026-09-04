using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.Intake.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Handlers;

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
