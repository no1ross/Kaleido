namespace Kaleido.Registry;

/// <summary>A processor's published steps and available transport bindings.</summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessRegistryItem
{
    /// <summary>The service publishing this processor.</summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>The published service version.</summary>
    public string? ServiceVersion { get; init; }

    /// <summary>The service version's availability.</summary>
    public string Status { get; init; } = RegistryStatusNames.NotReported;

    /// <summary>The processor's published name.</summary>
    public required string Name { get; init; }

    /// <summary>The processor's description, when supplied.</summary>
    public string? Description { get; init; }

    /// <summary>The processor's human-readable name, when supplied.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Whether consumers should start a new process with this processor.</summary>
    public bool IsEntryProcessor { get; init; }

    /// <summary>The operations exposed for this processor by its installed transports.</summary>
    public RegistryTransportBindings Bindings { get; init; } = new();

    /// <summary>Steps that can start a process.</summary>
    public IReadOnlyCollection<ProcessStepSummary> InitialSteps { get; init; } = [];

    /// <summary>Detailed metadata for the processor's steps.</summary>
    public IReadOnlyCollection<ProcessStepResponse>? Steps { get; init; } = [];
}

/// <summary>Common metadata published for a process step or a reference to one.</summary>
[ExcludeFromCodeCoverage]
public abstract record ProcessStepContract
{
    /// <summary>The step's registered name.</summary>
    public required string Name { get; init; }

    /// <summary>What the step does, when declared.</summary>
    public string? Description { get; init; }

    /// <summary>The step's human-readable name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Whether the step may run again after completion.</summary>
    public bool Repeatable { get; init; }

    /// <summary>Whether this step answers an information request.</summary>
    public bool IsInformationStep { get; init; }

    /// <summary>Whether the step may cause irreversible or loss-causing effects.</summary>
    public bool Destructive { get; init; }

    /// <summary>Whether the step changes something outside the process state.</summary>
    public bool External { get; init; }

    /// <summary>Whether clients should confirm before calling this step.</summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>The step's effective authorization rule, when declared.</summary>
    public AuthorizationMetadata? Authorization { get; init; }

    /// <summary>The operations exposed for this step by its installed transports.</summary>
    public RegistryTransportBindings Bindings { get; init; } = new();
}

/// <summary>Full published step detail, including relationships, inputs and results.</summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepResponse : ProcessStepContract
{
    /// <summary>Whether the step can start a process.</summary>
    public bool IsInitial { get; init; }

    /// <summary>The step's input fields.</summary>
    public IReadOnlyCollection<ProcessFieldMetadata> Fields { get; init; } = [];

    /// <summary>Steps required to run before this step.</summary>
    public IReadOnlyCollection<ProcessStepSummary> Dependencies { get; init; } = [];

    /// <summary>Steps after which this step becomes available.</summary>
    public IReadOnlyCollection<ProcessStepSummary> AvailableAfter { get; init; } = [];

    /// <summary>Steps that close availability of this step.</summary>
    public IReadOnlyCollection<ProcessStepSummary> AvailableUntil { get; init; } = [];

    /// <summary>Metadata for this step's typed result, when any.</summary>
    public ProcessStepResultMetadata? Result { get; init; }
}

/// <summary>A step named by a processor's initial steps or another step's relationships.</summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepSummary : ProcessStepContract;

/// <summary>A process step input field as published for discovery.</summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessFieldMetadata
{
    /// <summary>The input's human-readable label, when declared.</summary>
    public string? DisplayName { get; init; }

    /// <summary>A suggested prompt for collecting the input.</summary>
    public string? Prompt { get; init; }

    /// <summary>A lookup providing the input, when declared.</summary>
    public ProcessFieldLookup? Lookup { get; init; }

    /// <summary>A notice when the lookup is unavailable to this caller.</summary>
    public KaleidoNotice? Notice { get; init; }

    /// <summary>Validation constraints on the input.</summary>
    public IReadOnlyCollection<ConstraintMetadata> Constraints { get; init; } = [];

    /// <summary>The input's registered field name.</summary>
    public required string Name { get; init; }

    /// <summary>The input's description, when declared.</summary>
    public string? Description { get; init; }

    /// <summary>The input's portable data type.</summary>
    public required DataTypeMetadata DataType { get; init; }

    /// <summary>Data classification labels on the input.</summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];
}

/// <summary>A published link to the query supplying one process input.</summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessFieldLookup
{
    /// <summary>The service publishing the query.</summary>
    public required string Service { get; init; }

    /// <summary>The source's published name.</summary>
    public required string Source { get; init; }

    /// <summary>The source's view, when used.</summary>
    public string? View { get; init; }

    /// <summary>The result field supplying this input.</summary>
    public required string Field { get; init; }

    /// <summary>Inputs with the same group come from one result.</summary>
    public required string Group { get; init; }

    /// <summary>Result fields that identify a choice to a person.</summary>
    public IReadOnlyList<string> Display { get; init; } = [];
}

/// <summary>A field of a step's typed result.</summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessOutputFieldMetadata
{
    /// <summary>The returned field name.</summary>
    public required string Name { get; init; }

    /// <summary>The returned field description, when declared.</summary>
    public string? Description { get; init; }

    /// <summary>The returned field's portable data type.</summary>
    public required DataTypeMetadata DataType { get; init; }

    /// <summary>Data classification labels on the returned field.</summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];
}

/// <summary>The field descriptions of a step's typed result.</summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepResultMetadata
{
    /// <summary>The fields of the step's result.</summary>
    public IReadOnlyCollection<ProcessOutputFieldMetadata> OutputFields { get; init; } = [];
}
