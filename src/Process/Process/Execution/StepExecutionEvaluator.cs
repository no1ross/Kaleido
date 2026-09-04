using Kaleido.Process.Context;
using Kaleido.Process.Planning;
using Kaleido.Process.Registry;

namespace Kaleido.Process.Execution;

internal sealed class StepExecutionEvaluator : IStepExecutionEvaluator
{
    private readonly IStepAvailabilityResolver _availabilityResolver;
    private readonly IProcessorRegistry _processorRegistry;

    public StepExecutionEvaluator(
        IStepAvailabilityResolver availabilityResolver,
        IProcessorRegistry processorRegistry)
    {
        ArgumentNullException.ThrowIfNull(availabilityResolver);
        ArgumentNullException.ThrowIfNull(processorRegistry);

        _availabilityResolver = availabilityResolver;
        _processorRegistry = processorRegistry;
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

        if (result.RequiredStep is not null)
        {
            return EvaluateRequiredStep(
                currentCandidate,
                result.RequiredStep,
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
        ProcessStepReference requiredStep,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context)
    {
        var currentProcessorName =
            _processorRegistry.Registrations
                .Single()
                .Name;

        // If the required step belongs to an external processor, skip local
        // availability validation — we cannot evaluate it against our own graph.
        if (!string.Equals(
                requiredStep.ProcessorName,
                currentProcessorName,
                StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionDecision.AwaitingRequiredStep(
                requiredStep);
        }

        var availableSteps =
            _availabilityResolver.Resolve(
                currentCandidate,
                candidates,
                context);

        if (!availableSteps.Any(x =>
                string.Equals(
                    x.StepName,
                    requiredStep.StepName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return ExecutionDecision.ProcessViolation(
                StepProcessingMessage.Error(
                    StepProcessingMessageCode.RequiredStepNotAllowed,
                    $"'{requiredStep.StepName}' is not a valid next step from '{currentCandidate.StepName}'."));
        }

        var nextCandidate =
            candidates.FirstOrDefault(
                x => string.Equals(
                    x.StepName,
                    requiredStep.StepName,
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
                        a.StepName,
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
