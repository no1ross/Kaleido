
namespace Kaleido.Process.Execution;

[ExcludeFromCodeCoverage]
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

    public static ExecutionDecision Continue(
        StepCandidate nextCandidate)
        => new()
        {
            Type = ExecutionDecisionType.Continue,
            NextCandidate = nextCandidate
        };

    public static ExecutionDecision Complete()
        => new()
        {
            Type = ExecutionDecisionType.Complete
        };

    public static ExecutionDecision BusinessFailure()
        => new()
        {
            Type = ExecutionDecisionType.BusinessFailure
        };

    public static ExecutionDecision ProcessViolation(
        StepProcessingMessage message)
        => new()
        {
            Type = ExecutionDecisionType.ProcessViolation,
            Messages = [message]
        };

    public static ExecutionDecision AwaitingRequiredStep(
        string requiredStep,
        string? targetProcessorName = null)
        => new()
        {
            Type = ExecutionDecisionType.AwaitingRequiredStep,
            RequiredStep = requiredStep,
            TargetProcessorName = targetProcessorName
        };

    public static ExecutionDecision AwaitingStepSelection(
        IReadOnlyCollection<string> availableSteps)
        => new()
        {
            Type = ExecutionDecisionType.AwaitingStepSelection,
            AvailableSteps = availableSteps
        };

    public static ExecutionDecision HandOff(string targetProcessorName)
        => new()
        {
            Type = ExecutionDecisionType.HandOff,
            TargetProcessorName = targetProcessorName
        };
}
