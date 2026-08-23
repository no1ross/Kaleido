using Kaleido.Process.Participant.Context;

namespace Kaleido.Process.Participant.Execution;

public sealed record ProcessStepContext
(
    Guid ProcessId,
    StepContext StepContext,
    IReadOnlyCollection<string> AvailableNextSteps
);
