using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Samples.PriorAuth.Member.Queryable.ContextSources;
using Kaleido.Samples.PriorAuth.Member.Queryable.ViewSources;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    DisplayName = "Capture Member",
    Description = "Creates or updates the prior authorization with the selected member.",
    Version = "1.0.0")]
[AvailableAfter<StartRadiologyIntakeStep>]
[AvailableUntil<CaptureRequestingProviderStep>]
// See ValidateMemberStep: the member cannot be changed once the MRI/CT questions are answered.
[AvailableUntil<CaptureMriInfoStep>]
[AvailableUntil<ConfirmCtInsteadOfMriStep>]
[Repeatable]
public sealed record CaptureMemberStep : IProcessStep
{
    [Required]
    [Description("The member the request is for, found with Member Search by the member ID or first and last name, with the date of birth. Several members can match (twins on a family plan share the member ID): read back the matches (name, member ID, plan, coverage dates) and let the caller pick the member this request is for.")]
    [Display(Name = "Member", Prompt = "What is the member's ID, first and last name, and date of birth?")]
    [Lookup(
        Service = "member",
        Source = nameof(MemberQueryContextSource),
        View = nameof(MemberSearchViewSource),
        Field = "MemberId",
        Display = ["DisplayName", "DateOfBirth", "MemberNumber", "PlanName", "EffectiveDate", "TerminationDate"])]
    public required Guid MemberId { get; init; }

    [Required]
    [Description("The member's enrollment (plan coverage) from the same Member Search result.")]
    [Lookup(
        Service = "member",
        Source = nameof(MemberQueryContextSource),
        View = nameof(MemberSearchViewSource),
        Field = "MemberEnrollmentId")]
    public required Guid MemberEnrollmentId { get; init; }

    [Required]
    [Description("The date the service will be rendered.")]
    [Display(Name = "Date of service", Prompt = "What is the date of service?")]
    public required DateOnly DateOfService { get; init; }
}
