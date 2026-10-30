using System.Text.Json;
using System.Text.Json.Serialization;
using Kaleido.Http.Registry;
using Kaleido.Observability;

namespace Kaleido.Http.Processor;

// ── Requests ────────────────────────────────────────────────────────────────

/// <summary>
/// The body of the multi-step execute endpoint (<c>POST /{service}/processes/execute</c>): one or
/// more steps submitted together, so a consumer can send all the information it currently has and
/// let the process determine what can happen next.
/// </summary>
/// <remarks>
/// The process id is not part of the body: an existing process is continued by sending its id in the
/// <c>X-Kaleido-Process-Id</c> request header; without it a new process is started. Every submitted
/// step is authorized against its own declared requirement before anything runs, and the first
/// denial rejects the whole request. Then every step is read into its step type: an unknown step
/// (400, <c>pro_unknown_step</c>) or an input that does not fit it (400, <c>argument_error</c> naming
/// the step and the JSON path) rejects the whole request, so nothing runs and no process is created.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record ExecuteProcessRequest
{
    /// <summary>
    /// The submitted steps, each with its input. Step names are matched case-insensitively and must
    /// be unique within the request; a step named twice rejects the request (400,
    /// <c>pro_invalid_request</c>) before anything runs. Defaults to an empty collection.
    /// </summary>
    public IReadOnlyCollection<ProcessStepRequest> Steps
    {
        get;
        init;
    }
        = [];
}

/// <summary>One step submitted in an <see cref="ExecuteProcessRequest"/>: the step to run and its input.</summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepRequest
{
    /// <summary>
    /// The step's published name (its type name, as listed in the registry), matched
    /// case-insensitively against the processor's registered steps.
    /// </summary>
    public required string StepName
    {
        get;
        init;
    }

    /// <summary>
    /// The step's input as raw JSON: the same object that the per-step endpoint takes as
    /// <c>processStep</c>, read into the step type with the same rules.
    /// </summary>
    public required JsonElement Request
    {
        get;
        init;
    }
}

/// <summary>
/// The body of a per-step execute endpoint (<c>POST /{service}/processes/steps/{step}</c>): an
/// envelope such as <c>{"processStep":{"field":"value"}}</c>.
/// </summary>
/// <remarks>
/// The route selects the step, so neither the step name nor the process id is a body field. An
/// existing process is continued by sending its id in the <c>X-Kaleido-Process-Id</c> request
/// header; without it a new process is started.
/// </remarks>
/// <typeparam name="TProcessStep">
/// The step's input type: the step type itself on the server; on a client, the step type, a local
/// mirror with the same shape, or <see cref="JsonElement"/>.
/// </typeparam>
[ExcludeFromCodeCoverage]
public sealed record ExecuteStepRequest<TProcessStep>
{
    /// <summary>The step's input.</summary>
    public required TProcessStep ProcessStep
    {
        get;
        init;
    }
}

// ── Execution responses ──────────────────────────────────────────────────────

/// <summary>
/// The response of the multi-step execute endpoint (<see cref="ExecuteProcessRequest"/>): where the
/// process stands after the run, and the results of the steps that ran.
/// </summary>
/// <remarks>
/// An HTTP 200 does not by itself mean every submitted step completed. Each step's recorded
/// status is published by the process state endpoint (<see cref="ProcessStateResponse.Steps"/>).
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record ProcessExecutionResponse
{
    /// <summary>
    /// The id of the process that was created or continued; also returned in the
    /// <c>X-Kaleido-Process-Id</c> response header.
    /// </summary>
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

    /// <summary>
    /// The steps on the local processor that the caller may run next, each with its execute URL.
    /// Empty when no step is available.
    /// </summary>
    public IReadOnlyCollection<ProcessStepSummary> AvailableSteps
    {
        get;
        init;
    }
        = [];

    /// <summary>
    /// One entry for each step of the run that is no longer pending or that produced business
    /// messages or runtime diagnostics; steps left pending without any message are omitted.
    /// </summary>
    public IReadOnlyCollection<ProcessExecutionStepResponse> Results
    {
        get;
        init;
    }
        = [];
}

/// <summary>The result of one step in a <see cref="ProcessExecutionResponse"/>.</summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessExecutionStepResponse
{
    /// <summary>The step's registered name (its type name).</summary>
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

    /// <summary>
    /// The step handler's response, or an empty object (<c>{}</c>) when the step produced none.
    /// Serialized as the response's runtime type; a client reads it back as JSON.
    /// </summary>
    public required object Response
    {
        get;
        init;
    }
}

/// <summary>
/// The response of a per-step execute endpoint (<see cref="ExecuteStepRequest{TProcessStep}"/>) for
/// a step without a typed result; the base of <see cref="StepExecutionResponse{TResponse}"/>.
/// </summary>
/// <remarks>
/// An HTTP 200 does not mean the step completed: inspect <see cref="Status"/>.
/// </remarks>
[ExcludeFromCodeCoverage]
public record StepExecutionResponse
{
    /// <summary>
    /// The id of the process that was created or continued; also returned in the
    /// <c>X-Kaleido-Process-Id</c> response header.
    /// </summary>
    public required Guid ProcessId
    {
        get;
        init;
    }

    /// <summary>The executed step's registered name (its type name).</summary>
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

    /// <summary>
    /// What happened to the executed step: <see cref="StepExecutionStatus.Completed"/>,
    /// <see cref="StepExecutionStatus.Rejected"/> or <see cref="StepExecutionStatus.ValidationFailed"/>
    /// (correct the input), <see cref="StepExecutionStatus.Failed"/> (a business failure),
    /// <see cref="StepExecutionStatus.Exception"/> (a defect; retry later),
    /// <see cref="StepExecutionStatus.Canceled"/>, or <see cref="StepExecutionStatus.Pending"/> when it
    /// did not run (the framework messages say why). The step's status, not the HTTP status, says
    /// whether it completed.
    /// </summary>
    public StepExecutionStatus Status
    {
        get;
        init;
    }

    /// <summary>
    /// The steps on the local processor that the caller may run next, each with its execute URL.
    /// Empty when no step is available.
    /// </summary>
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

/// <summary>
/// The response of a per-step execute endpoint for a step whose handler returns a typed result:
/// the fields of <see cref="StepExecutionResponse"/> plus <see cref="Result"/>.
/// </summary>
/// <typeparam name="TResponse">
/// The step's result type: the handler's response type on the server; on a client, that type, a
/// local mirror, or <see cref="JsonElement"/>.
/// </typeparam>
[ExcludeFromCodeCoverage]
public sealed record StepExecutionResponse<TResponse> : StepExecutionResponse
{
    /// <summary>
    /// The step handler's typed response; <see langword="null"/> (or the default value) when the
    /// step produced no response.
    /// </summary>
    public TResponse? Result
    {
        get;
        init;
    }
}

// ── State response ───────────────────────────────────────────────────────────

/// <summary>
/// The current state of a process, returned by the process state endpoint
/// (<c>GET /{service}/processes/{processId}</c>). Reading the state never executes a step.
/// </summary>
/// <remarks>
/// The endpoint answers 404 for an unknown process id; the client's
/// <see cref="IKaleidoProcessorClient.GetProcessStateAsync"/> returns <see langword="null"/> then.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record ProcessStateResponse
{
    /// <summary>The process's id.</summary>
    public required Guid ProcessId
    {
        get;
        init;
    }

    /// <summary>
    /// The process's current execution state, as stored: for example waiting for a required step,
    /// waiting for information, complete, or handed off to another processor.
    /// </summary>
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

    /// <summary>
    /// The steps on the local processor that the caller may run next, each with its execute URL.
    /// Empty when no step is available.
    /// </summary>
    public IReadOnlyCollection<ProcessStepSummary> AvailableSteps
    {
        get;
        init;
    }
        = [];

    /// <summary>
    /// The recorded state of each step tracked by the process, ordered by step name
    /// (case-insensitive). A step that never ran is listed with its pending status.
    /// </summary>
    public IReadOnlyCollection<ProcessStepHistory> Steps
    {
        get;
        init;
    }
        = [];

    /// <summary>When the process was created (UTC); set once.</summary>
    public DateTimeOffset CreatedUtc
    {
        get;
        init;
    }

    /// <summary>
    /// When the runtime last changed the process state (UTC), for example when a step's outcome
    /// was applied.
    /// </summary>
    public DateTimeOffset UpdatedUtc
    {
        get;
        init;
    }

    /// <summary>
    /// The caller that created this process, captured from the authenticated principal at
    /// creation. <see langword="null"/> when the process was created anonymously.
    /// </summary>
    /// <remarks>
    /// A tag only (created by), never an access rule: Kaleido has no
    /// case-level guardrails, so any caller admitted by a step's declared
    /// roles/policies may read or continue the process (ADR 0010).
    /// </remarks>
    public KaleidoCaller? CreatedBy
    {
        get;
        init;
    }

    /// <summary>
    /// The caller of the latest request that ran a step of this process (a tag, like
    /// <see cref="CreatedBy"/>).
    /// </summary>
    public KaleidoCaller? UpdatedBy
    {
        get;
        init;
    }
}

/// <summary>
/// The recorded state of one step of a process, in <see cref="ProcessStateResponse.Steps"/>: its
/// latest status and when, and by whom, it was first and last executed.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepHistory
{
    /// <summary>The step's registered name (its type name).</summary>
    public required string StepName
    {
        get;
        init;
    }

    /// <summary>
    /// The step's last recorded execution status; <see cref="StepExecutionStatus.Pending"/> while
    /// it has not been processed.
    /// </summary>
    public required StepExecutionStatus Status
    {
        get;
        init;
    }

    /// <summary>When the step was first executed; null when it never ran.</summary>
    public DateTimeOffset? FirstExecuted
    {
        get;
        init;
    }

    /// <summary>The caller of the step's first execution (a tag).</summary>
    public KaleidoCaller? StartedBy
    {
        get;
        init;
    }

    /// <summary>When the step was last executed; null when it never ran.</summary>
    public DateTimeOffset? LastExecuted
    {
        get;
        init;
    }

    /// <summary>The caller of the step's most recent execution (a tag).</summary>
    public KaleidoCaller? LastUpdatedBy
    {
        get;
        init;
    }
}

// ── Registry / step responses ────────────────────────────────────────────────

/// <summary>
/// A service's processor as published in the registry (<c>GET /{service}/registry</c>): the
/// service's identity, the steps that can start a process, and the full detail of every step.
/// </summary>
/// <remarks>
/// The registry is filtered per caller: steps the caller is not authorized to run are removed, and a
/// processor whose steps were all removed is not listed.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record ProcessorRegistryResponse
{
    /// <summary>
    /// The service name — matches <see cref="KaleidoServiceOptions.ServiceName"/>.
    /// Allows consumers to identify which service this processor belongs to.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>The processor's name: the service name, the same value as <see cref="ServiceName"/>.</summary>
    public required string Name { get; init; }

    /// <summary>The service's description (<see cref="KaleidoServiceOptions.Description"/>), if any.</summary>
    public string? Description { get; init; }

    /// <summary>The service's human-readable name (<see cref="KaleidoServiceOptions.DisplayName"/>), if any.</summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// True when this processor is the entry point consumers should start with
    /// (<see cref="KaleidoServiceOptions.IsEntryProcessor"/>); only one processor in a distributed
    /// system should set it.
    /// </summary>
    public bool IsEntryProcessor { get; init; }

    /// <summary>The service's registry URL (<c>/{service}/registry</c>).</summary>
    public string RegistryUrl { get; init; }
        = string.Empty;

    /// <summary>
    /// The steps that can start a new process (no dependencies and no availability preconditions;
    /// never an information step), ordered by name, each with its <c>ExecuteUrl</c>.
    /// </summary>
    public IReadOnlyCollection<ProcessStepSummary> InitialSteps { get; init; }
        = [];

    /// <summary>
    /// Full step detail for every registered step, omitted (null) when the registry carries none for
    /// this processor; treat a missing value like an empty one.
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

/// <summary>
/// An input field of a step as published in the registry (<see cref="ProcessStepResponse.Fields"/>):
/// its name, data type, constraints, classifications and optional UI hints and lookup.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessFieldMetadata
{
    /// <summary>
    /// A short label for the input, from <c>[Display(Name = …)]</c>. A hint for UIs and AI agents,
    /// which may phrase the question their own way.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>A suggested prompt, from <c>[Display(Prompt = …)]</c>. A hint, like <see cref="DisplayName"/>.</summary>
    public string? Prompt { get; init; }

    /// <summary>
    /// Where the input's value comes from, from <c>[Lookup]</c>: a field of one result of a query.
    /// Null when the user supplies the value directly.
    /// </summary>
    public ProcessFieldLookup? Lookup { get; init; }

    /// <summary>The input's validation constraints, one per validation attribute; empty when it declares none.</summary>
    public IReadOnlyCollection<ConstraintMetadata> Constraints { get; init; } = [];

    /// <summary>The field's name: the step type's property name.</summary>
    public required string Name { get; init; }

    /// <summary>The field's description (<c>[Description]</c>, or <c>[Display(Description = …)]</c>); null when neither is declared.</summary>
    public string? Description { get; init; }

    /// <summary>The field's published data type.</summary>
    public required DataTypeMetadata DataType { get; init; }

    /// <summary>
    /// The field's data classifications (<c>DataClassificationAttribute</c>, e.g. <c>"PriorAuth:PHI"</c>),
    /// so clients know which values are sensitive; empty when the field is not classified.
    /// </summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];
}

/// <summary>
/// A step input's lookup (<see cref="ProcessFieldMetadata.Lookup"/>): the input takes
/// <see cref="Field"/> of one result of the query <see cref="Service"/>/<see cref="Source"/>[/<see cref="View"/>];
/// the inputs of a step with the same <see cref="Group"/> are filled from the same result.
/// </summary>
/// <remarks>A published link, never a choice: the user identifies and picks the result.</remarks>
[ExcludeFromCodeCoverage]
public sealed record ProcessFieldLookup
{
    /// <summary>The service that publishes the query.</summary>
    public required string Service { get; init; }

    /// <summary>The query source's published name.</summary>
    public required string Source { get; init; }

    /// <summary>The view of <see cref="Source"/>, or null for a direct source query.</summary>
    public string? View { get; init; }

    /// <summary>The result field the input takes.</summary>
    public required string Field { get; init; }

    /// <summary>The step's inputs with the same group are filled from the same result.</summary>
    public required string Group { get; init; }

    /// <summary>The result fields that identify a result to a person, in display order (may be empty).</summary>
    public IReadOnlyList<string> Display { get; init; } = [];
}

/// <summary>
/// A field of a step's typed result as published in the registry
/// (<see cref="ProcessStepResultMetadata.OutputFields"/>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessOutputFieldMetadata
{
    /// <summary>The field's name: the result type's property name.</summary>
    public required string Name { get; init; }

    /// <summary>The field's description (<c>[Description]</c>), if any.</summary>
    public string? Description { get; init; }

    /// <summary>The field's published data type.</summary>
    public required DataTypeMetadata DataType { get; init; }

    /// <summary>
    /// The field's data classifications (<c>DataClassificationAttribute</c>, e.g. <c>"PriorAuth:PHI"</c>);
    /// empty when the field is not classified.
    /// </summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];
}

/// <summary>
/// The shape of a step's typed result as published in the registry
/// (<see cref="ProcessStepResponse.Result"/>); absent for a step whose handler returns no typed response.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepResultMetadata
{
    /// <summary>The fields of the step's result.</summary>
    public IReadOnlyCollection<ProcessOutputFieldMetadata> OutputFields { get; init; }
        = [];
}
