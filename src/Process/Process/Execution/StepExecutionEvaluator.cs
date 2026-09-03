using Kaleido.Process.Context;
using Kaleido.Process.Planning;
using Kaleido.Process.Registry;

namespace Kaleido.Process.Execution;

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
        ProcessorContext context)
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
        ProcessorContext context)
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
        ProcessorContext context)
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