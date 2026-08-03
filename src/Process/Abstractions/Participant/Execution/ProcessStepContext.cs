using Kaleido.Process.Participant.Context;

namespace Kaleido.Process.Participant.Execution;

public sealed record ProcessStepContext
(
    StepContext StepContext,
    IReadOnlyCollection<string> AvailableNextSteps
);
