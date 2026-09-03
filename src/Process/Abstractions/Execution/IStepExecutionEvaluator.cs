using Kaleido.Process.Context;
using Kaleido.Process.Planning;

namespace Kaleido.Process.Execution;

internal interface IStepExecutionEvaluator
{
    ExecutionDecision Evaluate(
        StepCandidate currentCandidate,
        ProcessStepInvokerResult result,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context);
}