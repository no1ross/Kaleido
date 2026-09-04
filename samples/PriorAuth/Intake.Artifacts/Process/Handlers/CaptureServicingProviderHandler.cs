using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.Intake.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Handlers;

public sealed class CaptureServicingProviderHandler
    : IProcessStepHandler<CaptureServicingProviderStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureServicingProviderStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            ProcessStepHandlerResult.Success());
    }
}
