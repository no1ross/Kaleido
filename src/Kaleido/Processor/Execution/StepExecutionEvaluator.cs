using System.Globalization;
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

        if (Concluded(currentCandidate, result) is { } concluded)
        {
            return concluded;
        }

        if (result.RequiredStep is not null)
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
            currentCandidate,
            candidates,
            context);
    }

    // A result that decides the process by itself, whatever the step graph offers; null otherwise.
    private ExecutionDecision? Concluded(
        StepCandidate currentCandidate,
        StepInvocationResult result)
    {
        if (result.Rejected)
        {
            return ExecutionDecision.Rejected();
        }

        if (!result.Succeeded)
        {
            return ExecutionDecision.BusinessFailure();
        }

        // The handler completed the whole process: nothing more runs, whatever the rules still offer (#534).
        if (result.CompletesProcess)
        {
            return ExecutionDecision.Complete();
        }

        // A handoff gives the process to another service; this service itself is not one (#522).
        if (result.TargetProcessorName is not { } targetProcessorName)
        {
            return null;
        }

        return string.Equals(targetProcessorName, serviceOptions.ServiceName, StringComparison.OrdinalIgnoreCase)
            ? ExecutionDecision.ProcessViolation(
                StepProcessingMessage.Error(
                    ProcessorErrorCodes.InvalidHandOff,
                    $"'{currentCandidate.StepName}' handed the process off to '{targetProcessorName}', which is this service; hand off to another service, or continue with a step of this one."))
            : ExecutionDecision.HandOff(targetProcessorName);
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
                    $"The information request from '{currentCandidate.StepName}' for '{requiredStep}' is not well-formed: {string.Join(' ', problems.Select(x => x.Message))}"));
        }

        // Every time questions are put is a new exchange, whatever the handler set (#442): answers to
        // an earlier exchange of the same questionnaire can then be told apart.
        return IsAvailable(currentCandidate, requiredStep, candidates, context)
            ? ExecutionDecision.AwaitingInformation(
                requiredStep,
                informationRequest with { ExchangeId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) })
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
        StepCandidate currentCandidate,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context)
    {
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

        // Offered steps never include information steps: they are only reachable as the
        // required step, with an information request (RequireInformation).
        var offeredSteps =
            availableSteps
                .Where(x => registry.Find(x)?.IsInformationStep != true)
                .ToArray();

        if (offeredSteps.Length > 0)
        {
            return ExecutionDecision.AwaitingStepSelection(
                offeredSteps);
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
