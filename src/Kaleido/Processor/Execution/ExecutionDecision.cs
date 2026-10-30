
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

    public string? TargetProcessorName
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
        string requiredStep,
        string? targetProcessorName = null)
        => new()
        {
            Type = ExecutionDecisionType.AwaitingRequiredStep,
            RequiredStep = requiredStep,
            TargetProcessorName = targetProcessorName,
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

    public static ExecutionDecision HandOff(string targetProcessorName)
        => new()
        {
            Type = ExecutionDecisionType.HandOff,
            TargetProcessorName = targetProcessorName,
        };
}
