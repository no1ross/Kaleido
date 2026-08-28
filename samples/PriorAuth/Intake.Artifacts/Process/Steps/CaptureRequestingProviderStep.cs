using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Steps;

[ProcessStep(
    Name = "CaptureRequestingProvider",
    DisplayName = "Intake - Capture Requesting Provider",
    Description = "Captures the requesting provider for the current prior authorization.",
    Version = "1.0.0")]
[AvailableAfter(typeof(CaptureRequestedServiceStep))]
[Repeatable]
public sealed record CaptureRequestingProviderStep
{
    [Required]
    public Guid ProviderId { get; init; }

    public Guid? ProviderLocationId { get; init; }

    [Required]
    [StringLength(200)]
    public string ProviderName { get; init; } = string.Empty;

    [StringLength(200)]
    public string? LocationName { get; init; }
}
