using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Planning;

namespace Kaleido.Process.Participant.Execution;

internal interface IStepExecutionEvaluator
{
    ExecutionDecision Evaluate(
        StepCandidate currentCandidate,
        ProcessStepInvokerResult result,
        IReadOnlyCollection<StepCandidate> candidates,
        ParticipantContext context);
}