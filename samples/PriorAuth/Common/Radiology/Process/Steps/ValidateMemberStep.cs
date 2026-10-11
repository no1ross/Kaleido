using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Samples.PriorAuth.Member.Queryable.ContextSources;
using Kaleido.Samples.PriorAuth.Member.Queryable.ViewSources;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    DisplayName = "Validate Member",
    Description = "Validates member eligibility for the current prior authorization. " +
                  "Requires the radiology intake to have been started. " +
                  "Checks that the member exists and has active enrollment for the date of service.",
    Version = "1.0.0")]
[AvailableAfter<StartRadiologyIntakeStep>]
[AvailableUntil<CaptureRequestingProviderStep>]
[Repeatable]
public sealed record ValidateMemberStep : IProcessStep
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
    public Guid MemberId { get; init; }

    [Required]
    [Description("The member's enrollment (plan coverage) from the same Member Search result.")]
    [Lookup(
        Service = "member",
        Source = nameof(MemberQueryContextSource),
        View = nameof(MemberSearchViewSource),
        Field = "MemberEnrollmentId")]
    public Guid MemberEnrollmentId { get; init; }

    [Required]
    [Description("The date the service will be rendered.")]
    [Display(Name = "Date of service", Prompt = "What is the date of service?")]
    public DateOnly DateOfService { get; init; }
}
