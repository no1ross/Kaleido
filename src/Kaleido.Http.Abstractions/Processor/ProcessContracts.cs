using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaleido.Http.Processor;

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
                            [stepName] = ProcessStep,
                        },
                },
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
    /// The next required step on the local processor, including its pending information
    /// request when the process is <see cref="ProcessExecutionState.AwaitingInformation"/>.
    /// Null when <see cref="TargetProcessorName"/> is set — call the target processor's
    /// state endpoint instead to get the authoritative required step.
    /// </summary>
    public ProcessRequiredStep? RequiredStep
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

    /// <summary>Handler-authored messages for consumers, independent of framework diagnostics.</summary>
    public IReadOnlyCollection<ProcessMessage> BusinessMessages
    {
        get;
        init;
    }
        = [];

    /// <summary>Framework diagnostics, empty unless the transport explicitly enables them.</summary>
    public IReadOnlyCollection<ProcessMessage> FrameworkMessages
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
    /// The next required step on the local processor, including its pending information
    /// request when the process is <see cref="ProcessExecutionState.AwaitingInformation"/>.
    /// Null when <see cref="TargetProcessorName"/> is set — call the target processor's
    /// state endpoint instead to get the authoritative required step.
    /// </summary>
    public ProcessRequiredStep? RequiredStep
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

    /// <summary>Handler-authored messages for consumers, independent of framework diagnostics.</summary>
    public IReadOnlyCollection<ProcessMessage> BusinessMessages
    {
        get;
        init;
    }
        = [];

    /// <summary>Framework diagnostics, empty unless the transport explicitly enables them.</summary>
    public IReadOnlyCollection<ProcessMessage> FrameworkMessages
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
    /// The next required step on the local processor, including its pending information
    /// request when the process is <see cref="ProcessExecutionState.AwaitingInformation"/>.
    /// Null when <see cref="TargetProcessorName"/> is set — call the target processor's
    /// state endpoint instead to get the authoritative required step.
    /// </summary>
    public ProcessRequiredStep? RequiredStep
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
    /// Name of the caller that created this process, captured from the
    /// authenticated principal at creation. <see langword="null"/> when the process was
    /// created anonymously.
    /// </summary>
    /// <remarks>
    /// A tag only (created by), never an access rule: Kaleido has no
    /// case-level guardrails, so any caller admitted by a step's declared
    /// roles/policies may read or continue the process (ADR 0010).
    /// </remarks>
    public string? Owner
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

/// <summary>
/// What every step on the wire carries: the full step in the registry, and the summaries that name a
/// step elsewhere (initial steps, relationships, next steps, the required step).
/// </summary>
[ExcludeFromCodeCoverage]
public abstract record ProcessStepContract
{
    /// <summary>The step's name: its type name.</summary>
    public required string Name { get; init; }

    /// <summary>What the step does and when it applies.</summary>
    public string? Description { get; init; }

    /// <summary>A short, human-readable name for the step.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The step's version.</summary>
    public string? Version { get; init; }

    /// <summary>True when the step may run again after it completed.</summary>
    public bool Repeatable { get; init; }

    /// <summary>
    /// True when the step's input is the answers to an information request; its fields are then
    /// empty and the pending request (on the required step) says what to answer.
    /// </summary>
    public bool IsInformationStep { get; init; }

    /// <summary>The step is irreversible or loss-causing (cancel, delete, withdraw, pay); declared by the implementer.</summary>
    public bool Destructive { get; init; }

    /// <summary>The step changes something outside the process state (another system's data, a message, a payment); declared by the implementer.</summary>
    public bool External { get; init; }

    /// <summary>The user should confirm before a client calls the step (a hint; not enforced).</summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Authorization requirement for this step. Null means open.
    /// </summary>
    public AuthorizationMetadata? Authorization { get; init; }

    /// <summary>Where to execute the step.</summary>
    public string ExecuteUrl { get; init; }
        = string.Empty;
}

/// <summary>A step as published in the processor registry: its description, inputs, relationships and result.</summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepResponse : ProcessStepContract
{
    /// <summary>
    /// True when this step is an entry point — it has no dependencies and no
    /// availability preconditions, so it can start a new process instance.
    /// </summary>
    public bool IsInitial { get; init; }

    /// <summary>The step's input fields; empty for an information step.</summary>
    public IReadOnlyCollection<ProcessFieldMetadata> Fields { get; init; }
        = [];

    /// <summary>The steps this step depends on.</summary>
    public IReadOnlyCollection<ProcessStepSummary> Dependencies { get; init; }
        = [];

    /// <summary>The steps after which this step becomes available.</summary>
    public IReadOnlyCollection<ProcessStepSummary> AvailableAfter { get; init; }
        = [];

    /// <summary>The steps that close this step.</summary>
    public IReadOnlyCollection<ProcessStepSummary> AvailableUntil { get; init; }
        = [];

    /// <summary>The step's result fields, when it returns a typed response.</summary>
    public ProcessStepResultMetadata? Result { get; init; }
}

/// <summary>A step named from elsewhere: an initial step, a relationship or a next step.</summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepSummary : ProcessStepContract;

/// <summary>
/// The step a process requires next: the same shape as each <c>AvailableSteps</c> entry, plus the
/// pending <see cref="InformationRequest"/> when the step is an information step.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessRequiredStep : ProcessStepContract
{
    /// <summary>
    /// The questions to answer, submitted as this step's payload
    /// (<c>{ "informationRequestId": …, "items": [ … ] }</c>); null for an ordinary step.
    /// Question and display text is delivered verbatim: do not paraphrase it.
    /// </summary>
    public InformationRequest? InformationRequest { get; init; }
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
