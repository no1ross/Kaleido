using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Samples.PriorAuth.Radiology.Queryable.ContextSources;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    DisplayName = "Capture Servicing Provider",
    Description = "Captures the servicing provider: the facility where the service will be performed. Its network status is recorded with it.",
    Version = "2.0.0")]
[AvailableAfter<CaptureRequestingProviderStep>]
[AvailableUntil<SubmitPriorAuthorizationStep>]
[Repeatable]
public sealed record CaptureServicingProviderStep : IProcessStep
{
    [Required]
    [Description("The servicing facility, found with Servicing Provider Search from the facility name, NPI or location the caller gives.")]
    [Display(Name = "Servicing provider", Prompt = "Where will the service be performed?")]
    [Lookup(
        Service = "radiology",
        Source = nameof(ServicingProviderSearchSource),
        Field = "ProviderId",
        Display = ["ProviderName", "LocationName", "City", "StateCode", "PrimaryNpi", "IsInNetwork"])]
    public Guid ProviderId { get; init; }

    [Description("The facility's location from the same Servicing Provider Search result.")]
    [Lookup(Service = "radiology", Source = nameof(ServicingProviderSearchSource), Field = "ProviderLocationId")]
    public Guid? ProviderLocationId { get; init; }

    [Required]
    [StringLength(200)]
    [Description("The facility's name from the same Servicing Provider Search result.")]
    [Lookup(Service = "radiology", Source = nameof(ServicingProviderSearchSource), Field = "ProviderName")]
    public string ProviderName { get; init; } = string.Empty;

    [StringLength(200)]
    [Description("The location's name from the same Servicing Provider Search result.")]
    [Lookup(Service = "radiology", Source = nameof(ServicingProviderSearchSource), Field = "LocationName")]
    public string? LocationName { get; init; }

    [Description("Whether the facility is in the member's network, from the same Servicing Provider Search result.")]
    [Lookup(Service = "radiology", Source = nameof(ServicingProviderSearchSource), Field = "IsInNetwork")]
    public bool IsInNetwork { get; init; }
}