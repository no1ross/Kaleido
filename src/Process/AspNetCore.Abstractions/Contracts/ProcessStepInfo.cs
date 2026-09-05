namespace Kaleido.Process.AspNetCore.Contracts;

/// <summary>
/// Represents a step reference in an execution response.
/// Used for <c>RequiredStep</c> and <c>AvailableSteps</c> on execution
/// and state responses — where a step may belong to a different processor.
///
/// Unlike <see cref="ProcessStepSummary"/>, which is used in registry and
/// catalog responses and always describes a local step, this type carries
/// explicit <see cref="ProcessorName"/> and <see cref="StepName"/> fields
/// so the consumer can unambiguously identify the owning processor.
///
/// For local steps, <see cref="ExecuteUrl"/> and <see cref="MetadataUrl"/>
/// are populated. For external processor steps they are empty — the consumer
/// is expected to resolve them via its own registry using
/// <see cref="ProcessorName"/> and <see cref="StepName"/>.
/// </summary>
public sealed record ProcessStepInfo
{
    /// <summary>The name of the processor that owns this step.</summary>
    public required string ProcessorName { get; init; }

    /// <summary>The name of the step.</summary>
    public required string StepName { get; init; }

    /// <summary>
    /// The URL to execute this step.
    /// Empty when the step belongs to an external processor.
    /// </summary>
    public string ExecuteUrl { get; init; } = string.Empty;

    /// <summary>
    /// The URL to retrieve metadata for this step.
    /// Empty when the step belongs to an external processor.
    /// </summary>
    public string MetadataUrl { get; init; } = string.Empty;
}
