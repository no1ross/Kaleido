using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

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
[ExcludeFromCodeCoverage]
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
    /// When the process is waiting for a specific next step on the local processor,
    /// this contains the step name. Null when <see cref="TargetProcessorName"/> is set.
    /// </summary>
    public string? RequiredStep
    {
        get;
        init;
    }

    /// <summary>
    /// When set, the process has been handed off to this processor.
    /// The consumer must call the target processor's state endpoint to continue.
    /// </summary>
    public string? TargetProcessorName
    {
        get;
        init;
    }

    /// <summary>
    /// The currently available next steps on the local processor
    /// that may be supplied by the caller.
    /// </summary>
    public IReadOnlyCollection<string> AvailableSteps
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
[ExcludeFromCodeCoverage]
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

internal sealed class ProcessContextStore : IProcessContextStore
{
    private readonly ILogger<ProcessContextStore> _logger;
    private readonly ConcurrentDictionary<Guid, ProcessorContext> _contexts = new();

    public ProcessContextStore(
        ILogger<ProcessContextStore> logger)
    {
        _logger = logger;

        logger.LogWarning(
            "ProcessContextStore is active. This store has no eviction policy and will grow " +
            "without bound in long-running processes. Register a durable IProcessContextStore " +
            "(e.g. UseSqliteProcessContextStore) before deploying to production.");
    }

    public Task<ProcessorContext?> LoadAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _contexts.TryGetValue(processId, out var context);
        return Task.FromResult(context);
    }

    public Task SaveAsync(ProcessorContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();

        _contexts[context.ProcessId] = context;

        _logger.LogDebug(
            "Process context saved for process {ProcessId} ({StepCount} steps).",
            context.ProcessId,
            context.Steps.Count);

        return Task.CompletedTask;
    }
}
