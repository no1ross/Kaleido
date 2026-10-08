namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

/// <summary>
/// Confirms that a CT, rather than an MRI, is being requested. An information step: the
/// confirmation question is presented with it.
/// </summary>
[ProcessStep(
    DisplayName = "Confirm CT Instead Of MRI",
    Description = "Confirms that a CT scan, not an MRI, is being requested.",
    Version = "2.0.0")]
[AvailableAfter<StartRadiologyIntakeStep>]
[AvailableUntil<CaptureRequestingProviderStep>]
[Repeatable]
public sealed record ConfirmCtInsteadOfMriStep : IInformationStep
{
    public required string InformationRequestId { get; init; }

    public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
}
