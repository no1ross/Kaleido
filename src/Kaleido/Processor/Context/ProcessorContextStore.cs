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
/// <para>
/// Represents the current durable state of a process instance.
/// This object contains only the information required to continue
/// processing future requests.
/// </para>
/// <para>
/// Historical activity and operational evidence are emitted as
/// process events and should not be stored here.
/// </para>
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
    /// <see langword="null"/> when the process was created anonymously or on a transport
    /// without an authenticated caller.
    /// </summary>
    /// <remarks>
    /// A tag only (created by), never enforced. Kaleido has no case-level
    /// guardrails: whether a caller may open or continue a particular process
    /// is a business/data-level concern of the underlying systems, and a
    /// process may be continued by different callers (e.g. started through an
    /// IVR, taken over by a phone rep, finished by the provider in a portal),
    /// subject only to each step's declared roles/policies (ADR 0010).
    /// </remarks>
    public string? Owner
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
/// <para>Represents the current state of an individual process step.</para>
/// <para>
/// This is intentionally a lightweight summary and should not
/// contain historical information. Operational history is emitted
/// through process events.
/// </para>
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

internal sealed partial class ProcessorContextStore : IProcessorContextStore
{
    private readonly ILogger<ProcessorContextStore> _logger;
    private readonly ConcurrentDictionary<Guid, ProcessorContext> _contexts = new();

    public ProcessorContextStore(
        ILogger<ProcessorContextStore> logger)
    {
        _logger = logger;

        LogInMemoryStoreActive(logger);
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

        LogSaved(_logger, context.ProcessId, context.Steps.Count);

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "ProcessorContextStore is active. This store has no eviction policy and will grow without bound in long-running processes. Register a durable IProcessorContextStore (e.g. UseSqliteProcessorContextStore) before deploying to production.")]
    private static partial void LogInMemoryStoreActive(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Process context saved for process {ProcessId} ({StepCount} steps).")]
    private static partial void LogSaved(ILogger logger, Guid processId, int stepCount);
}
