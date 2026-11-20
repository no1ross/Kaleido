using Kaleido.Process.Context;

namespace Kaleido.Process;

/// <summary>
/// A transport-neutral read of a process's current state, without its durable store version or
/// internal request ids. Callable step operations belong to the Registry, not this snapshot.
/// </summary>
public sealed record ProcessStateSnapshot
{
    /// <summary>The process instance.</summary>
    public required Guid ProcessId { get; init; }

    /// <summary>The service that owns this process.</summary>
    public required string ServiceName { get; init; }

    /// <summary>The owner's route/state version, including a nullable value for a legacy stored process.</summary>
    public string? ServiceVersion { get; init; }

    /// <summary>The durable process execution state.</summary>
    public ProcessExecutionState State { get; init; }

    /// <summary>The step the process requires next, by name, or null when none is required.</summary>
    public string? RequiredStep { get; init; }

    /// <summary>The questions the required information step needs answered, when any.</summary>
    public InformationRequest? RequiredInformationRequest { get; init; }

    /// <summary>The service taking over a handed-off process, when any.</summary>
    public string? TargetProcessorName { get; init; }

    /// <summary>The target's version for a handed-off process, required to resume it there.</summary>
    public string? TargetProcessorVersion { get; init; }

    /// <summary>The names currently offered by this service's stored process state when awaiting step selection; not all Registry-published steps. A later read of a hand-off origin does not replay the target's immediate next-step answer: use <see cref="TargetProcessorName"/> and <see cref="TargetProcessorVersion"/> to read the target's state.</summary>
    public IReadOnlyCollection<string> AvailableSteps { get; init; } = [];

    /// <summary>A caller-scoped notice, when a host cannot expose the next steps to its caller.</summary>
    public KaleidoNotice? Notice { get; init; }

    /// <summary>The current status and execution evidence of the process's steps.</summary>
    public IReadOnlyCollection<ProcessStateStep> Steps { get; init; } = [];

    /// <summary>When the process was created.</summary>
    public DateTimeOffset CreatedUtc { get; init; }

    /// <summary>When a saved process transition most recently changed its state.</summary>
    public DateTimeOffset UpdatedUtc { get; init; }

    /// <summary>The caller that created this process, if known.</summary>
    public KaleidoCaller? CreatedBy { get; init; }

    /// <summary>The caller that most recently changed this process, if known.</summary>
    public KaleidoCaller? UpdatedBy { get; init; }

    /// <summary>Projects durable state into a neutral snapshot; hosts authorize visible steps before exposing it to callers.</summary>
    /// <param name="context">The stored state of the process.</param>
    /// <returns>The process's current state without store-only concurrency or request identifiers.</returns>
    public static ProcessStateSnapshot FromContext(ProcessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ProcessStateSnapshot
        {
            ProcessId = context.ProcessId,
            ServiceName = context.ProcessorName,
            ServiceVersion = context.ProcessorVersion,
            State = context.State,
            RequiredStep = context.RequiredStep,
            RequiredInformationRequest = context.RequiredInformationRequest,
            TargetProcessorName = context.TargetProcessorName,
            TargetProcessorVersion = context.TargetProcessorVersion,
            AvailableSteps = [.. context.AvailableSteps],
            Steps = [.. context.Steps.OrderBy(step => step.StepName, StringComparer.OrdinalIgnoreCase).Select(step => new ProcessStateStep
            {
                StepName = step.StepName,
                Status = step.Status,
                FirstExecuted = step.FirstExecuted,
                StartedBy = step.StartedBy,
                LastExecuted = step.LastExecuted,
                LastUpdatedBy = step.LastUpdatedBy,
            })],
            CreatedUtc = context.CreatedUtc,
            UpdatedUtc = context.UpdatedUtc,
            CreatedBy = context.CreatedBy,
            UpdatedBy = context.UpdatedBy,
        };
    }
}
