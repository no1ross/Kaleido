using Kaleido.Processor.Context;

namespace Kaleido.Processor.Planning;

/// <summary>
/// Admits only next steps: nothing on an ended process (complete, failed, handed off: #512); otherwise
/// the first step a request executes must be the pending required step or, when nothing is required,
/// a step available under the process rules (the initial steps for a new process). Answers to an information request are checked against the pending
/// request here, before the step runs. Later steps in the same request are chained by the
/// execution evaluator as today.
/// </summary>
internal interface IStepCandidateNextStepChecker
{
    void Check(
        IReadOnlyCollection<StepCandidate> orderedCandidates,
        ProcessorContext context);
}

internal sealed class StepCandidateNextStepChecker(
    IStepAvailabilityResolver availabilityResolver,
    IInformationValidator informationValidator)
    : IStepCandidateNextStepChecker
{
    public void Check(
        IReadOnlyCollection<StepCandidate> orderedCandidates,
        ProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(orderedCandidates);
        ArgumentNullException.ThrowIfNull(context);

        // An ended process admits nothing (#512): the state, not the step graph, decides.
        if (Ended(context) is { } ended)
        {
            foreach (var candidate in orderedCandidates.Where(x => x.IncludedInExecutionPlan))
            {
                Reject(
                    candidate,
                    StepProcessingMessage.Error(
                        ProcessorErrorCodes.ProcessEnded,
                        $"Process step '{candidate.StepName}' cannot run: {ended}"));
            }

            return;
        }

        var allowed =
            context.RequiredStep is { } requiredStep
                ? [requiredStep]
                : availabilityResolver.ResolveCurrent(context);

        foreach (var candidate in orderedCandidates.Where(x => x.IncludedInExecutionPlan))
        {
            if (!allowed.Contains(candidate.StepName, StringComparer.OrdinalIgnoreCase))
            {
                Reject(
                    candidate,
                    StepProcessingMessage.Error(
                        ProcessorErrorCodes.StepNotAvailable,
                        context.RequiredStep is null
                            ? $"Process step '{candidate.StepName}' is not available now. Available: {Describe(allowed)}."
                            : $"Process step '{candidate.StepName}' cannot run now: the process requires '{context.RequiredStep}' next."));

                continue;
            }

            if (candidate.Registration?.IsInformationStep == true)
            {
                var problems = CheckAnswers(candidate, context);

                if (problems.Count > 0)
                {
                    Reject(candidate, [.. problems]);
                    continue;
                }
            }

            // The first executable candidate is admitted; the evaluator chains the rest.
            return;
        }
    }

    private IReadOnlyCollection<StepProcessingMessage> CheckAnswers(
        StepCandidate candidate,
        ProcessorContext context)
    {
        if (context.RequiredInformationRequest is not { } pending ||
            !string.Equals(context.RequiredStep, candidate.StepName, StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                StepProcessingMessage.Error(
                    ProcessorErrorCodes.InformationResponseMismatch,
                    $"Information step '{candidate.StepName}' has no pending information request to answer."),
            ];
        }

        return candidate.Step is IInformationStep answers
            ? informationValidator.ValidateResponse(pending, answers)
            : throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Information step candidate '{candidate.StepName}' has no hydrated {nameof(IInformationStep)}.");
    }

    // Why the process admits no further step of this service, or null while it continues here. A
    // handoff back from the service that holds the process reopens it before planning (ProcessorRuntime).
    private static string? Ended(
        ProcessorContext context) =>
        context.State switch
        {
            ProcessExecutionState.Complete =>
                "the process is complete.",
            ProcessExecutionState.BusinessFailure =>
                "the process ended with a business failure; start a new process.",
            ProcessExecutionState.HandOff =>
                $"the process was handed off to '{context.TargetProcessorName}'; continue it there.",
            _ => null,
        };

    private static void Reject(
        StepCandidate candidate,
        params StepProcessingMessage[] messages)
    {
        candidate.IncludedInExecutionPlan = false;
        candidate.Status = StepCandidateStatus.Invalid;

        foreach (var message in messages)
        {
            candidate.AddMessage(message);
        }
    }

    private static string Describe(
        IReadOnlyCollection<string> steps) =>
        steps.Count == 0
            ? "none"
            : string.Join(", ", steps.Order(StringComparer.OrdinalIgnoreCase));
}
