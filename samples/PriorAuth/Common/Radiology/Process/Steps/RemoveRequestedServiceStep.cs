using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Samples.PriorAuth.Radiology.Queryable.ContextSources;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    DisplayName = "Remove Requested Service",
    Description = "Removes a requested service from the current prior authorization.",
    Version = "1.0.0",
    Destructive = true,
    RequiresConfirmation = true)]
[AvailableAfter<StartRadiologyIntakeStep>]
[AvailableUntil<CaptureRequestingProviderStep>]
[Repeatable]
public sealed record RemoveRequestedServiceStep : IProcessStep
{
    [Required]
    [Description("The requested service to remove, found with Requested Services for this request.")]
    [Display(Name = "Requested service", Prompt = "Which requested service should be removed?")]
    [Lookup(
        Service = "radiology",
        Source = nameof(RequestedServiceQueryContextSource),
        Field = "PriorAuthorizationRequestedServiceId",
        Display = ["ResolvedCodeValue", "Description"])]
    public required Guid PriorAuthorizationRequestedServiceId { get; init; }
}
