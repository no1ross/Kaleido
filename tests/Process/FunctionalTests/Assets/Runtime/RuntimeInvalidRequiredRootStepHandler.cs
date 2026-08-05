using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;

namespace Kaleido.Process.FunctionalTests.Assets.Runtime;

public sealed class RuntimeInvalidRequiredRootStepHandler :
    IProcessStepHandler<RuntimeInvalidRequiredRootStep, RuntimeInvalidRequiredRootStepResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeInvalidRequiredRootStepResponse>> ExecuteAsync(
        RuntimeInvalidRequiredRootStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeInvalidRequiredRootStepResponse>.Success(
                new RuntimeInvalidRequiredRootStepResponse(),
                RuntimeStepNames.Merge));
    }
}
