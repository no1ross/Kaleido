using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaleido.Http.Process;

// ── Requests ────────────────────────────────────────────────────────────────

[ExcludeFromCodeCoverage]
public sealed record ExecuteProcessRequest
{
    public IReadOnlyCollection<ProcessStepRequest> Steps
    {
        get;
        init;
    }
        = [];
}

[ExcludeFromCodeCoverage]
public sealed record ProcessStepRequest
{
    public required string StepName
    {
        get;
        init;
    }

    public required JsonElement Request
    {
        get;
        init;
    }
}

[ExcludeFromCodeCoverage]
public sealed record ExecuteStepRequest<TProcessStep>
{
    public required TProcessStep ProcessStep
    {
        get;
        init;
    }

    public ProcessRequest ToProcessRequest(
        string stepName,
        Guid? processId = null)
    {
        return new ProcessRequest
        {
            ProcessId = processId,

            Processor =
                new ProcessorRequest
                {
                    Steps =
                        new Dictionary<string, object?>(
                            StringComparer.OrdinalIgnoreCase)
                        {
                            [stepName] = ProcessStep
                        }
                }
        };
    }
}

// ── Execution responses ──────────────────────────────────────────────────────

[ExcludeFromCodeCoverage]
public sealed record ProcessExecutionResponse
{
    public required Guid ProcessId
    {
        get;
        init;
    }

    /// <summary>
    /// The next required step on the local processor.
    /// Null when <see cref="TargetProcessorName"/> is set — call the target processor's
    /// state endpoint instead to get the authoritative required step.
    /// </summary>
    public string? RequiredStep
    {
        get;
        init;
    }

    /// <summary>
    /// When set, the process has been handed off to this processor.
    /// The consumer must call GET /{TargetProcessorName}/processes/{ProcessId} to continue.
    /// <see cref="RequiredStep"/> will be null in this case.
    /// </summary>
    public string? TargetProcessorName
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessStepSummary> AvailableSteps
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<ProcessExecutionStepResponse> Results
    {
        get;
        init;
    }
        = [];
}

[ExcludeFromCodeCoverage]
public sealed record ProcessExecutionStepResponse
{
    public required string StepName
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessMessage> Messages
    {
        get;
        init;
    }
        = [];

    public required object Response
    {
        get;
        init;
    }
}

[ExcludeFromCodeCoverage]
public record StepExecutionResponse
{
    public required Guid ProcessId
    {
        get;
        init;
    }

    public required string StepName
    {
        get;
        init;
    }

    /// <summary>
    /// The next required step on the local processor.
    /// Null when <see cref="TargetProcessorName"/> is set — call the target processor's
    /// state endpoint instead to get the authoritative required step.
    /// </summary>
    public string? RequiredStep
    {
        get;
        init;
    }

    /// <summary>
    /// When set, the process has been handed off to this processor.
    /// The consumer must call GET /{TargetProcessorName}/processes/{ProcessId} to continue.
    /// <see cref="RequiredStep"/> will be null in this case.
    /// </summary>
    public string? TargetProcessorName
    {
        get;
        init;
    }

    public StepExecutionOutcome Outcome
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessStepSummary> AvailableSteps
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<ProcessMessage> Messages
    {
        get;
        init;
    }
        = [];
}

[ExcludeFromCodeCoverage]
public sealed record StepExecutionResponse<TResponse> : StepExecutionResponse
{
    public TResponse? Result
    {
        get;
        init;
    }
}

// ── State response ───────────────────────────────────────────────────────────

[ExcludeFromCodeCoverage]
public sealed record ProcessStateResponse
{
    public required Guid ProcessId
    {
        get;
        init;
    }

    public ProcessExecutionState State
    {
        get;
        init;
    }

    /// <summary>
    /// The next required step on the local processor.
    /// Null when <see cref="TargetProcessorName"/> is set — call the target processor's
    /// state endpoint instead to get the authoritative required step.
    /// </summary>
    public string? RequiredStep
    {
        get;
        init;
    }

    /// <summary>
    /// When set, the process has been handed off to this processor.
    /// The consumer must call GET /{TargetProcessorName}/processes/{ProcessId} to continue.
    /// <see cref="RequiredStep"/> will be null in this case.
    /// </summary>
    public string? TargetProcessorName
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessStepSummary> AvailableSteps
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<ProcessStepHistory> Steps
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

    /// <summary>
    /// Name of the caller that owns this process, captured from the
    /// authenticated principal at creation. <c>null</c> for unowned
    /// (anonymously created) processes.
    /// </summary>
    public string? Owner
    {
        get;
        init;
    }
}

/// <summary>Response for POST /{serviceName}/processes/{processId}/transfer.</summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessTransferResponse
{
    public required Guid ProcessId
    {
        get;
        init;
    }

    /// <summary>Caller name that now owns the process.</summary>
    public required string Owner
    {
        get;
        init;
    }
}

[ExcludeFromCodeCoverage]
public sealed record ProcessStepHistory
{
    public required string StepName
    {
        get;
        init;
    }

    public string Version
    {
        get;
        init;
    }
        = string.Empty;

    public required StepExecutionStatus Status
    {
        get;
        init;
    }

    public DateTimeOffset? LastExecuted
    {
        get;
        init;
    }
}

// ── Registry / step responses ────────────────────────────────────────────────

[ExcludeFromCodeCoverage]
public sealed record ProcessorRegistryResponse
{
    /// <summary>
    /// The service name — matches <see cref="KaleidoServiceOptions.ServiceName"/>.
    /// Allows consumers to identify which service this processor belongs to.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public bool IsEntryProcessor { get; init; }

    public string RegistryUrl { get; init; }
        = string.Empty;

    public IReadOnlyCollection<ProcessStepSummary> InitialSteps { get; init; }
        = [];

    /// <summary>
    /// Full step detail for every registered step.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyCollection<ProcessStepResponse>? Steps { get; init; }
        = [];
}

[ExcludeFromCodeCoverage]
public sealed record ProcessStepResponse
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public bool Repeatable { get; init; }

    /// <summary>
    /// True when this step is an entry point — it has no dependencies and no
    /// availability preconditions, so it can start a new process instance.
    /// </summary>
    public bool IsInitial { get; init; }

    /// <summary>
    /// Authorization requirement for this step. Null means open.
    /// </summary>
    public AuthorizationMetadata? Authorization { get; init; }

    public string ExecuteUrl { get; init; }
        = string.Empty;

    public string MetadataUrl { get; init; }
        = string.Empty;

    public IReadOnlyCollection<ProcessFieldMetadata> Fields { get; init; }
        = [];

    public IReadOnlyCollection<ProcessStepSummary> Dependencies { get; init; }
        = [];

    public IReadOnlyCollection<ProcessStepSummary> AvailableAfter { get; init; }
        = [];

    public IReadOnlyCollection<ProcessStepSummary> AvailableUntil { get; init; }
        = [];

    public ProcessStepResultMetadata? Result { get; init; }
}

[ExcludeFromCodeCoverage]
public sealed record ProcessStepSummary
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public bool Repeatable { get; init; }

    /// <summary>
    /// Authorization requirement for this step. Null means open.
    /// </summary>
    public AuthorizationMetadata? Authorization { get; init; }

    public string ExecuteUrl { get; init; }
        = string.Empty;

    public string MetadataUrl { get; init; }
        = string.Empty;
}

[ExcludeFromCodeCoverage]
public sealed record ProcessFieldMetadata : ProcessorInputFieldDescriptor;

[ExcludeFromCodeCoverage]
public sealed record ProcessOutputFieldMetadata : ProcessorOutputFieldDescriptor;

[ExcludeFromCodeCoverage]
public sealed record ProcessStepResultMetadata
{
    public IReadOnlyCollection<ProcessOutputFieldMetadata> OutputFields { get; init; }
        = [];
}
