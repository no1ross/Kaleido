using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Samples.PriorAuth.Member.Queryable.ContextSources;
using Kaleido.Samples.PriorAuth.Member.Queryable.ViewSources;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Steps;

[ProcessStep(
    DisplayName = "Intake - Capture Member",
    Description = "Records member information against the intake session for auditing and correlation.",
    Version = "1.0.0")]
[AvailableAfter<ValidateMemberStep>]
public sealed record CaptureMemberStep : IProcessStep
{
    [Required]
    [Description("The validated member: the same Member Search result the caller picked for Validate Member.")]
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
    [Description("The date the service will be rendered (the same date as Validate Member).")]
    [Display(Name = "Date of service", Prompt = "What is the date of service?")]
    public required DateOnly DateOfService { get; init; }
}
