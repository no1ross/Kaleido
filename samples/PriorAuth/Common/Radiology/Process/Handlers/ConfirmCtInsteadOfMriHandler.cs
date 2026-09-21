using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

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
