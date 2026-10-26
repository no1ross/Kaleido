using Kaleido.Processor;
using Kaleido.Processor.Execution;

namespace Kaleido.IntegrationTests.Transitions;

// The one step of the SqliteTransitionTests service: a service with process steps is what registers
// the Process runtime (and its transitions).
[ProcessStep(DisplayName = "Transition step", Description = "Lets the test service register the Process runtime.")]
public sealed class TransitionStep : IProcessStep;

public sealed record TransitionResult;

public sealed class TransitionStepHandler : IProcessStepHandler<TransitionStep, TransitionResult>
{
    public Task<ProcessStepHandlerResult<TransitionResult>> ExecuteAsync(
        TransitionStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ProcessStepHandlerResult.Success(new TransitionResult()));
}
