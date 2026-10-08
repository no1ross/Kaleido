using Kaleido.Processor.Context;
using Kaleido.Processor.Registry;

namespace Kaleido.Processor.Execution;

internal interface IStepExecutionEvaluator
{
    ExecutionDecision Evaluate(
        StepCandidate currentCandidate,
        StepInvocationResult result,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context);
}

internal sealed class StepExecutionEvaluator(
    IStepAvailabilityResolver availabilityResolver,
    IProcessorStepRegistry registry,
    IInformationValidator informationValidator,
    KaleidoServiceOptions serviceOptions)
    : IStepExecutionEvaluator
{

    public ExecutionDecision Evaluate(
        StepCandidate currentCandidate,
        StepInvocationResult result,
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
            var requiredRegistration =
                registry.Find(result.RequiredStep);

            if (requiredRegistration is null)
            {
                return ExecutionDecision.ProcessViolation(
                    StepProcessingMessage.Error(
                        ProcessorErrorCodes.RequiredStepNotAllowed,
                        $"'{result.RequiredStep.Name}' is not a registered step in this processor and cannot be required from '{currentCandidate.StepName}'."));
            }

            return requiredRegistration.IsInformationStep
                ? EvaluateRequiredInformation(
                    currentCandidate,
                    requiredRegistration.Metadata.Name,
                    result.InformationRequest,
                    candidates,
                    context)
                : EvaluateRequiredStep(
                    currentCandidate,
                    requiredRegistration.Metadata.Name,
                    candidates,
                    context);
        }

        return EvaluateAvailableSteps(
            result.TargetProcessorName,
            currentCandidate,
            candidates,
            context);
    }

    // An information step always waits for answers to the request presented now, so it never
    // continues straight into a candidate submitted in the same request.
    private ExecutionDecision EvaluateRequiredInformation(
        StepCandidate currentCandidate,
        string requiredStep,
        InformationRequest? informationRequest,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context)
    {
        if (informationRequest is null)
        {
            return ExecutionDecision.ProcessViolation(
                StepProcessingMessage.Error(
                    ProcessorErrorCodes.InformationRequestMissing,
                    $"'{requiredStep}' is an information step and must be required with an information request (RequireInformation), from '{currentCandidate.StepName}'."));
        }

        var problems =
            informationValidator.ValidateRequest(
                informationRequest);

        if (problems.Count > 0)
        {
            return ExecutionDecision.ProcessViolation(
                StepProcessingMessage.Error(
                    ProcessorErrorCodes.InformationRequestInvalid,
                    $"The information request from '{currentCandidate.StepName}' for '{requiredStep}' is not well-formed: {string.Join(" ", problems.Select(x => x.Message))}"));
        }

        return IsAvailable(currentCandidate, requiredStep, candidates, context)
            ? ExecutionDecision.AwaitingInformation(
                requiredStep,
                informationRequest)
            : NotAValidNextStep(currentCandidate, requiredStep);
    }

    private ExecutionDecision EvaluateRequiredStep(
        StepCandidate currentCandidate,
        string requiredStep,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context)
    {
        if (!IsAvailable(currentCandidate, requiredStep, candidates, context))
        {
            return NotAValidNextStep(currentCandidate, requiredStep);
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
                serviceOptions.ServiceName,
                StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionDecision.HandOff(targetProcessorName); ;
        }

        var availableSteps =
            availabilityResolver.Resolve(
                currentCandidate,
                candidates,
                context);

        // Information steps only run against a request presented in an earlier outcome, so a
        // pre-submitted information step is never picked up here.
        var nextCandidate =
            candidates.FirstOrDefault(
                x => x.Registration?.IsInformationStep != true &&
                     availableSteps.Any(a =>
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

    private bool IsAvailable(
        StepCandidate currentCandidate,
        string step,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context) =>
        availabilityResolver
            .Resolve(currentCandidate, candidates, context)
            .Any(x => string.Equals(x, step, StringComparison.OrdinalIgnoreCase));

    private static ExecutionDecision NotAValidNextStep(
        StepCandidate currentCandidate,
        string requiredStep) =>
        ExecutionDecision.ProcessViolation(
            StepProcessingMessage.Error(
                ProcessorErrorCodes.RequiredStepNotAllowed,
                $"'{requiredStep}' is not a valid next step from '{currentCandidate.StepName}'."));
}
