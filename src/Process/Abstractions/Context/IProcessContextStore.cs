using Kaleido.Process.Execution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Context;

public interface IProcessContextStore
{
    Task<ProcessorContext?> LoadAsync(Guid processId, CancellationToken cancellationToken = default);

    Task SaveAsync(ProcessorContext context, CancellationToken cancellationToken = default);
}




/// <summary>
/// Represents the current durable state of a process instance.
/// This object contains only the information required to continue
/// processing future requests.
///
/// Historical activity and operational evidence are emitted as
/// process events and should not be stored here.
/// </summary>
public sealed record ProcessorContext
{
    /// <summary>
    /// Uniquely identifies the process instance.
    /// </summary>
    public required Guid ProcessId
    {
        get;
        init;
    }

    /// <summary>
    /// The registered name of the processor that owns this process instance.
    /// </summary>
    public required string ProcessorName
    {
        get;
        init;
    }

    public string? LatestRequestId
    {
        get;
        init;
    }

    /// <summary>
    /// Current process execution state.
    /// </summary>
    public ProcessExecutionState State
    {
        get;
        init;
    }

    /// <summary>
    /// When the process is waiting for a specific next step,
    /// this contains the required step reference (processor name + step name).
    /// </summary>
    public ProcessStepReference? RequiredStep
    {
        get;
        init;
    }

    /// <summary>
    /// The currently available next steps that may be supplied
    /// by the caller.
    /// </summary>
    public IReadOnlyCollection<ProcessStepReference> AvailableSteps
    {
        get;
        init;
    }
        = [];
    
    /// <summary>
    /// Current state for each registered process step.
    /// </summary>
    public IReadOnlyCollection<StepContext> Steps
    {
        get;
        init;
    }
        = [];

    public DateTimeOffset CreatedUtc
    {
        get;
        init;
    }

    public DateTimeOffset UpdatedUtc
    {
        get;
        init;
    }
    
    public StepContext? FindStep(
        string stepName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

        return Steps.FirstOrDefault(
            x => string.Equals(
                x.StepName,
                stepName,
                StringComparison.OrdinalIgnoreCase));
    }

    public bool HasCompletedStep(
        string stepName)
    {
        return Steps.Any(
            x => string.Equals(
                x.StepName,
                stepName,
                StringComparison.OrdinalIgnoreCase)
            && x.Status == StepExecutionStatus.Completed);
    }
}

/// <summary>
/// Represents the current state of an individual process step.
///
/// This is intentionally a lightweight summary and should not
/// contain historical information. Operational history is emitted
/// through process events.
/// </summary>
public sealed record StepContext
{
    /// <summary>
    /// Unique process step name.
    /// </summary>
    public string StepName
    {
        get;
        init;
    }
        = string.Empty;

    /// <summary>
    /// Registered process step version.
    /// </summary>
    public string Version
    {
        get;
        init;
    }
        = string.Empty;

    /// <summary>
    /// Last known execution status for this step.
    /// </summary>
    public StepExecutionStatus Status
    {
        get;
        init;
    }

    /// <summary>
    /// Request identifier associated with the most recent
    /// update to this step.
    /// </summary>
    public string? LatestRequestId
    {
        get;
        init;
    }

    /// <summary>
    /// Timestamp of the most recent execution attempt.
    /// </summary>
    public DateTimeOffset? LastExecuted
    {
        get;
        init;
    }
}