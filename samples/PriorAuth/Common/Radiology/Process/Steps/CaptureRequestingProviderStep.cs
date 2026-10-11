using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Samples.PriorAuth.Radiology.Queryable.ContextSources;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

/// <remarks>
/// Only the ids of the chosen search result are taken; the handler checks that the location belongs
/// to the provider and is offered for this request, and reads the names and network status from that
/// result, so a caller cannot mix rows or supply its own values.
/// </remarks>
[ProcessStep(
    DisplayName = "Capture Requesting Provider",
    Description = "Captures the requesting provider for the current prior authorization.",
    Version = "2.0.0")]
[AvailableAfter<StartRadiologyIntakeStep>]
[AvailableUntil<SubmitPriorAuthorizationStep>]
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

    [Required]
    [Description("The provider's location from the same Requesting Provider Search result.")]
    [Lookup(Service = "radiology", Source = nameof(RequestingProviderSearchSource), Field = "ProviderLocationId")]
    public Guid ProviderLocationId { get; init; }
}
