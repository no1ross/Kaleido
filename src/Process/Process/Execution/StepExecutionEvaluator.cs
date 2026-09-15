using Kaleido.Process.Context;
using Kaleido.Process.Planning;

namespace Kaleido.Process.Execution;

internal sealed class StepExecutionEvaluator : IStepExecutionEvaluator
{
    private readonly IStepAvailabilityResolver _availabilityResolver;
    private readonly KaleidoServiceOptions _serviceOptions;

    public StepExecutionEvaluator(
        IStepAvailabilityResolver availabilityResolver,
        KaleidoServiceOptions serviceOptions)
    {
        ArgumentNullException.ThrowIfNull(availabilityResolver);
        ArgumentNullException.ThrowIfNull(serviceOptions);

        _availabilityResolver = availabilityResolver;
        _serviceOptions = serviceOptions;
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

        // Cross-processor handoff — check TargetProcessorName before RequiredStep.
        // A HandOff result has RequiredStep = null and TargetProcessorName set;
        // falling through to EvaluateAvailableSteps would silently drop the handoff.
        if (!string.IsNullOrEmpty(result.TargetProcessorName) && result.RequiredStep is null)
        {
            var currentProcessorName =
                _serviceOptions.ServiceName;

            if (!string.Equals(
                    result.TargetProcessorName,
                    currentProcessorName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return ExecutionDecision.HandOff(result.TargetProcessorName);
            }
        }

        if (result.RequiredStep is not null)
        {
            return EvaluateRequiredStep(
                currentCandidate,
                result.RequiredStep,
                result.TargetProcessorName,
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
        string? targetProcessorName,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context)
    {
        var currentProcessorName =
            _serviceOptions.ServiceName;

        // If the required step belongs to an external processor, skip local
        // availability validation — we cannot evaluate it against our own graph.
        if (!string.IsNullOrEmpty(targetProcessorName) &&
            !string.Equals(
                targetProcessorName,
                currentProcessorName,
                StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionDecision.AwaitingRequiredStep(
                requiredStep,
                targetProcessorName);
        }

        var availableSteps =
            _availabilityResolver.Resolve(
                currentCandidate,
                candidates,
                context);

        if (!availableSteps.Any(x =>
                string.Equals(
                    x,
                    requiredStep,
                    StringComparison.OrdinalIgnoreCase)))
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
                x => availableSteps.Any(a =>
                    string.Equals(
                        a,
                        x.StepName,
                        StringComparison.OrdinalIgnoreCase)));

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
