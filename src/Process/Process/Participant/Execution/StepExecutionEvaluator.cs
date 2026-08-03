using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;
using Kaleido.Process.Participant.Registry;

namespace Kaleido.Process.Participant.Execution;

internal interface IStepExecutionEvaluator
{
    ExecutionDecision Evaluate(
        StepCandidate currentCandidate,
        ProcessStepInvokerResult result,
        IReadOnlyCollection<StepCandidate> candidates);
}

internal sealed class StepExecutionEvaluator
    : IStepExecutionEvaluator
{
    private readonly IProcessStepRegistry _registry;

    public StepExecutionEvaluator(
        IProcessStepRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

    public ExecutionDecision Evaluate(
        StepCandidate currentCandidate,
        ProcessStepInvokerResult result,
        IReadOnlyCollection<StepCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(currentCandidate);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(candidates);

        if (!result.Succeeded)
        {
            return ExecutionDecision.BusinessFailure();
        }

        if (!string.IsNullOrWhiteSpace(result.RequiredStep))
        {
            return EvaluateRequiredStep(
                currentCandidate,
                result.RequiredStep!,
                candidates);
        }

        return EvaluateAvailableSteps(
            currentCandidate,
            candidates);
    }

    private ExecutionDecision EvaluateRequiredStep(
        StepCandidate currentCandidate,
        string requiredStep,
        IReadOnlyCollection<StepCandidate> candidates)
    {
        var availableSteps =
            GetAvailableNextSteps(
                currentCandidate);

        if (!availableSteps.Contains(
                requiredStep,
                StringComparer.OrdinalIgnoreCase))
        {
            return ExecutionDecision.ProcessViolation(
                StepProcessingMessage.Error(
                    StepProcessingMessageCode.RequiredStepNotAllowed,
                    $"'{requiredStep}' is not a valid next step from '{currentCandidate.StepName}'."));
        }

        var nextCandidate =
            candidates.FirstOrDefault(
                x => string.Equals(
                    x.StepName,
                    requiredStep,
                    StringComparison.OrdinalIgnoreCase));

        if (nextCandidate is null)
        {
            return ExecutionDecision.AwaitingRequiredStep(
                requiredStep);
        }

        return ExecutionDecision.Continue(
            nextCandidate);
    }

    private ExecutionDecision EvaluateAvailableSteps(
        StepCandidate currentCandidate,
        IReadOnlyCollection<StepCandidate> candidates)
    {
        var availableSteps =
            GetAvailableNextSteps(
                currentCandidate);

        var nextCandidate =
            candidates.FirstOrDefault(
                x => availableSteps.Contains(
                    x.StepName,
                    StringComparer.OrdinalIgnoreCase));

        if (nextCandidate is not null)
        {
            return ExecutionDecision.Continue(
                nextCandidate);
        }

        if (availableSteps.Count > 0)
        {
            return ExecutionDecision.AwaitingStepSelection(
                availableSteps);
        }

        return ExecutionDecision.Complete();
    }

    private IReadOnlyCollection<string> GetAvailableNextSteps(
        StepCandidate candidate)
    {
        return _registry
            .GetDependents(candidate.Registration!.StepType)
            .Select(x => x.Metadata.Name)
            .ToArray();
    }
}