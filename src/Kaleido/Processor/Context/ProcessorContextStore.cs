using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Kaleido.Processor.Context;

/// <summary>
/// Provides durable storage for <see cref="ProcessorContext"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation is an unbounded in-memory dictionary, suitable for development
/// and testing only. It does not survive process restarts and grows without bound in long-running
/// services. Register a durable implementation before deploying to production (e.g.
/// <c>UseSqliteProcessorContextStore</c> or a custom store).
/// </para>
/// <para>
/// <strong>Concurrency:</strong> This interface provides no optimistic concurrency or idempotency
/// guarantees. Two concurrent requests for the same process can overwrite each other's state.
/// Consumer implementations should enforce concurrency control (e.g. row versioning, etags)
/// appropriate to their storage backend.
/// </para>
/// </remarks>
public interface IProcessorContextStore
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
    /// Name of the caller that created this process, captured from
    /// <see cref="KaleidoCorrelationContext.CallerName"/> at creation.
    /// <c>null</c> means the process is unowned (created anonymously or on a
    /// transport without an authenticated caller) — open to any caller.
    /// </summary>
    public string? Owner
    {
        get;
        init;
    }

    /// <summary>
    /// Snapshot of the creator's roles at creation. Callers who share a role
    /// (teammates) may resume or claim the process alongside the owner.
    /// </summary>
    public IReadOnlyCollection<string> OwnerRoles
    {
        get;
        init;
    }
        = [];

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
    /// The pending information request: the questions <see cref="RequiredStep"/> (an
    /// <see cref="IInformationStep"/>) needs answered, stored as presented. Set only in
    /// <see cref="ProcessExecutionState.AwaitingInformation"/>; replaced or cleared by the next
    /// outcome. This is execution state only: recording the questions and answers over time is
    /// the implementer's concern.
    /// </summary>
    public InformationRequest? RequiredInformationRequest
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

internal sealed class ProcessorContextStore : IProcessorContextStore
{
    private readonly ILogger<ProcessorContextStore> _logger;
    private readonly ConcurrentDictionary<Guid, ProcessorContext> _contexts = new();

    public ProcessorContextStore(
        ILogger<ProcessorContextStore> logger)
    {
        _logger = logger;

        logger.LogWarning(
            "ProcessorContextStore is active. This store has no eviction policy and will grow " +
            "without bound in long-running processes. Register a durable IProcessorContextStore " +
            "(e.g. UseSqliteProcessorContextStore) before deploying to production.");
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
