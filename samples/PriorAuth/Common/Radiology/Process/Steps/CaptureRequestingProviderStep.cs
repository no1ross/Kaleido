using System.ComponentModel;
using Kaleido.Samples.PriorAuth.Radiology.Queryable.ContextSources;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    DisplayName = "Capture Requesting Provider",
    Description = "Captures the requesting provider for the current prior authorization.",
    Version = "1.0.0")]
[AvailableAfter<StartRadiologyIntakeStep>]
[Repeatable]
public sealed record CaptureRequestingProviderStep : IProcessStep
{
    [Required]
    [Description("The requesting provider, found with Requesting Provider Search (scoped to this request) from the provider name, NPI or location the caller gives.")]
    [Display(Name = "Requesting provider", Prompt = "Who is the requesting provider?")]
    [Lookup(
        Service = "radiology",
        Source = nameof(RequestingProviderSearchSource),
        Field = "ProviderId",
        Display = ["ProviderName", "LocationName", "City", "StateCode", "PrimaryNpi", "IsInNetwork"])]
    public Guid ProviderId { get; init; }

    [Description("The provider's location from the same Requesting Provider Search result.")]
    [Lookup(Service = "radiology", Source = nameof(RequestingProviderSearchSource), Field = "ProviderLocationId")]
    public Guid? ProviderLocationId { get; init; }

    [Required]
    [StringLength(200)]
    [Description("The provider's name from the same Requesting Provider Search result.")]
    [Lookup(Service = "radiology", Source = nameof(RequestingProviderSearchSource), Field = "ProviderName")]
    public string ProviderName { get; init; } = string.Empty;

    [StringLength(200)]
    [Description("The location's name from the same Requesting Provider Search result.")]
    [Lookup(Service = "radiology", Source = nameof(RequestingProviderSearchSource), Field = "LocationName")]
    public string? LocationName { get; init; }
}
