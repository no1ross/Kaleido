using Kaleido.Process.Context;

namespace Kaleido.Process.Execution;

public sealed record ProcessStepContext
(
    Guid ProcessId,
    StepContext StepContext,
    IReadOnlyCollection<ProcessStepReference> AvailableNextSteps
);
