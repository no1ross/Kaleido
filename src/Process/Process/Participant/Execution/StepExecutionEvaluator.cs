using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Planning;
using Kaleido.Process.Participant.Registry;

namespace Kaleido.Process.Participant.Execution;

internal sealed class StepExecutionEvaluator : IStepExecutionEvaluator
{
    private readonly IStepAvailabilityResolver _availabilityResolver;

    public StepExecutionEvaluator(
        IStepAvailabilityResolver availabilityResolver)
    {
        ArgumentNullException.ThrowIfNull(availabilityResolver);

        _availabilityResolver = availabilityResolver;
    }

    public ExecutionDecision Evaluate(
        StepCandidate currentCandidate,
        ProcessStepInvokerResult result,
        IReadOnlyCollection<StepCandidate> candidates,
        ParticipantContext context)
    {
        ArgumentNullException.ThrowIfNull(currentCandidate);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        if (!result.Succeeded)
        {
            return ExecutionDecision.BusinessFailure();
        }

        if (!string.IsNullOrWhiteSpace(result.RequiredStep))
        {
            return EvaluateRequiredStep(
                currentCandidate,
                result.RequiredStep!,
                candidates,
                context);
        }

        return EvaluateAvailableSteps(
            currentCandidate,
            candidates,
            context);
    }

    private ExecutionDecision EvaluateRequiredStep(
        StepCandidate currentCandidate,
        string requiredStep,
        IReadOnlyCollection<StepCandidate> candidates,
        ParticipantContext context)
    {
        var availableSteps =
            _availabilityResolver.Resolve(
                currentCandidate,
                candidates,
                context);

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
        IReadOnlyCollection<StepCandidate> candidates,
        ParticipantContext context)
    {
        var availableSteps =
            _availabilityResolver.Resolve(
                currentCandidate,
                candidates,
                context);

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

}