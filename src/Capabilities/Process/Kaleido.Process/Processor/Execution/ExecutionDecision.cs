
namespace Kaleido.Processor.Execution;

internal sealed record ExecutionDecision
{
    public required ExecutionDecisionType Type
    {
        get;
        init;
    }

    public StepCandidate? NextCandidate
    {
        get;
        init;
    }

    public string? RequiredStep
    {
        get;
        init;
    }

    public InformationRequest? InformationRequest
    {
        get;
        init;
    }

    // The hand-off (#198): the target, whose name the state keeps, and its answer, which the request
    // answers with and which is never saved.
    public HandOffContinuation? HandOff
    {
        get;
        init;
    }

    public IReadOnlyCollection<string> AvailableSteps
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<StepProcessingMessage> Messages
    {
        get;
        init;
    }
        = [];

    // The status of the step whose handler result this decision is: the one mapping the state, the
    // answer and the event share (#522, #536).
    public StepExecutionStatus StepStatus =>
        Type switch
        {
            ExecutionDecisionType.ProcessViolation => StepExecutionStatus.Exception,
            ExecutionDecisionType.BusinessFailure => StepExecutionStatus.Failed,
            ExecutionDecisionType.Rejected => StepExecutionStatus.Rejected,
            _ => StepExecutionStatus.Completed,
        };

    public static ExecutionDecision Continue(
        StepCandidate nextCandidate)
        => new()
        {
            Type = ExecutionDecisionType.Continue,
            NextCandidate = nextCandidate,
        };

    public static ExecutionDecision Complete()
        => new()
        {
            Type = ExecutionDecisionType.Complete,
        };

    public static ExecutionDecision BusinessFailure()
        => new()
        {
            Type = ExecutionDecisionType.BusinessFailure,
        };

    public static ExecutionDecision Rejected()
        => new()
        {
            Type = ExecutionDecisionType.Rejected,
        };

    public static ExecutionDecision ProcessViolation(
        StepProcessingMessage message)
        => new()
        {
            Type = ExecutionDecisionType.ProcessViolation,
            Messages = [message],
        };

    public static ExecutionDecision AwaitingRequiredStep(
        string requiredStep)
        => new()
        {
            Type = ExecutionDecisionType.AwaitingRequiredStep,
            RequiredStep = requiredStep,
        };

    public static ExecutionDecision AwaitingStepSelection(
        IReadOnlyCollection<string> availableSteps)
        => new()
        {
            Type = ExecutionDecisionType.AwaitingStepSelection,
            AvailableSteps = availableSteps,
        };

    public static ExecutionDecision AwaitingInformation(
        string requiredStep,
        InformationRequest informationRequest)
        => new()
        {
            Type = ExecutionDecisionType.AwaitingInformation,
            RequiredStep = requiredStep,
            InformationRequest = informationRequest,
        };

    // The target's framework messages become the step's runtime messages, so the transport's
    // framework-message option applies to them as to the origin's own (#198).
    public static ExecutionDecision HandOffTo(HandOffContinuation handOff)
        => new()
        {
            Type = ExecutionDecisionType.HandOff,
            HandOff = handOff,
            Messages =
            [
                .. handOff.FrameworkMessages.Select(x => new StepProcessingMessage
                {
                    Type = x.Type,
                    Code = x.Code,
                    Message = x.Message,
                }),
            ],
        };
}
