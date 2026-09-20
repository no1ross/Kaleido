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

        if (result.RequiredStep is not null && 
            string.IsNullOrEmpty(result.TargetProcessorName))
        {
            return EvaluateRequiredStep(
                currentCandidate,
                result.RequiredStep,
                candidates,
                context);
        }

        return EvaluateAvailableSteps(
            result.TargetProcessorName,
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
        string? targetProcessorName,
        StepCandidate currentCandidate,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context)
    {

        // Cross-processor handoff — check TargetProcessorName before RequiredStep.
        // A HandOff result has RequiredStep = set; and TargetProcessorName set;
        // falling through to EvaluateAvailableSteps would silently drop the handoff.
        if (!string.IsNullOrEmpty(targetProcessorName) &&
            !string.Equals(
                targetProcessorName,
                _serviceOptions.ServiceName,
                StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionDecision.HandOff(targetProcessorName); ;
        }

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
