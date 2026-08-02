using Kaleido.Process.Participant.Planning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Participant.Execution;

internal interface IStepExecutionPolicy
{
    ExecutionDecision? Evaluate(
        StepExecutionPolicyContext context);
}

internal sealed class BusinessFailureExecutionPolicy
    : IStepExecutionPolicy
{
    public ExecutionDecision? Evaluate(
        StepExecutionPolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Result.Succeeded)
        {
            return null;
        }

        var results =
            new List<StepExecutionResult>
            {
                new()
                {
                    Candidate = context.Candidate,
                    Status = StepExecutionStatus.Completed,
                    Result = context.Result
                }
            };

        foreach (var candidate in context.CandidatesToSkip)
        {
            results.Add(
                new StepExecutionResult
                {
                    Candidate = candidate,
                    Status = StepExecutionStatus.Skipped
                });
        }

        return new ExecutionDecision
        {
            Type = ExecutionDecisionType.Stop,
            Results = results
        };
    }
}

public sealed record StepExecutionPolicyContext
{
    public required StepCandidate Candidate
    {
        get;
        init;
    }

    public required ProcessStepResult Result
    {
        get;
        init;
    }

    public required IReadOnlyCollection<StepCandidate> CandidatesToSkip
    {
        get;
        init;
    }
}

public sealed record ProcessExecutionResult
{
    public required IReadOnlyCollection<StepExecutionResult> Steps
    {
        get;
        init;
    }
}


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

    public IReadOnlyCollection<StepExecutionResult> Results
    {
        get;
        init;
    }
        = [];
}

internal enum ExecutionDecisionType
{
    Continue,

    Jump,

    Stop,

    Complete
}

public sealed record StepExecutionResult
{
    public required StepCandidate Candidate
    {
        get;
        init;
    }

    public required StepExecutionStatus Status
    {
        get;
        init;
    }

    public ProcessStepResult? Result
    {
        get;
        init;
    }

    public IReadOnlyCollection<StepProcessingMessage> ProcessMessages
    {
        get;
        init;
    }
        = [];
}