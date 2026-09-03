using Kaleido.Process.Planning;

namespace Kaleido.Process.Execution;

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
        string requiredStep)
        => new()
        {
            Type = ExecutionDecisionType.AwaitingRequiredStep,
            RequiredStep = requiredStep
        };

    public static ExecutionDecision AwaitingStepSelection(
        IReadOnlyCollection<string> availableSteps)
        => new()
        {
            Type = ExecutionDecisionType.AwaitingStepSelection,
            AvailableSteps = availableSteps
        };
}


//public sealed record StepExecutionResult
//{
//    public required StepCandidate Candidate
//    {
//        get;
//        init;
//    }

//    public required StepExecutionStatus Status
//    {
//        get;
//        init;
//    }

//    public ProcessStepResult? Result
//    {
//        get;
//        init;
//    }

//    public IReadOnlyCollection<StepProcessingMessage> ProcessMessages
//    {
//        get;
//        init;
//    }
//        = [];
//}

//internal interface IStepExecutionPolicy
//{
//    ExecutionDecision? Evaluate(
//        StepExecutionPolicyContext context);
//}

//internal sealed class BusinessFailureExecutionPolicy
//    : IStepExecutionPolicy
//{
//    public ExecutionDecision? Evaluate(
//        StepExecutionPolicyContext context)
//    {
//        ArgumentNullException.ThrowIfNull(context);

//        if (context.Result.Succeeded)
//        {
//            return null;
//        }

//        var results =
//            new List<StepExecutionResult>
//            {
//                new()
//                {
//                    Candidate = context.Candidate,
//                    Status = StepExecutionStatus.Completed,
//                    Result = context.Result
//                }
//            };

//        foreach (var candidate in context.CandidatesToSkip)
//        {
//            results.Add(
//                new StepExecutionResult
//                {
//                    Candidate = candidate,
//                    Status = StepExecutionStatus.Skipped
//                });
//        }

//        return new ExecutionDecision
//        {
//            Type = ExecutionDecisionType.Stop,
//            Results = results
//        };
//    }
//}

//public sealed record StepExecutionPolicyContext
//{
//    public required StepCandidate Candidate
//    {
//        get;
//        init;
//    }

//    public required ProcessStepResult Result
//    {
//        get;
//        init;
//    }

//    public required IReadOnlyCollection<StepCandidate> CandidatesToSkip
//    {
//        get;
//        init;
//    }
//}

//public sealed record ProcessExecutionResult
//{
//    public required IReadOnlyCollection<StepExecutionResult> Steps
//    {
//        get;
//        init;
//    }
//}