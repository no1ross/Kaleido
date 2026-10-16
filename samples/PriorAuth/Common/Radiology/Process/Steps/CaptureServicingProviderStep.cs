using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Samples.PriorAuth.Radiology.Queryable.ContextSources;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

/// <remarks>
/// Only the ids of the chosen search result are taken; the handler checks that the location belongs
/// to the facility and is offered for this request, and reads the names and network status from that
/// result, so a caller cannot mix rows or claim a network status.
/// </remarks>
[ProcessStep(
    DisplayName = "Capture Servicing Provider",
    Description = "Captures the servicing provider: the facility where the service will be performed. Its network status is recorded with it.",
    Version = "3.0.0")]
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
    public required Guid ProviderId { get; init; }

    [Required]
    [Description("The facility's location from the same Servicing Provider Search result.")]
    [Lookup(Service = "radiology", Source = nameof(ServicingProviderSearchSource), Field = "ProviderLocationId")]
    public required Guid ProviderLocationId { get; init; }
}
