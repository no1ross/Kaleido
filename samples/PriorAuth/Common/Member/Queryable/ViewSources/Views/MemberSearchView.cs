using System.ComponentModel;
using Kaleido.Samples.PriorAuth.Compliance;

namespace Kaleido.Samples.PriorAuth.Member.Queryable.ViewSources.Views;

public sealed record MemberSearchView
{
    [Description("The unique ID of the member.")]
    public Guid MemberId { get; init; }

    [Description("The unique ID of the member's enrollment (plan coverage) this row describes.")]
    public Guid MemberEnrollmentId { get; init; }

    [Description("The member's first name.")]
    [Phi]
    public string FirstName { get; init; } = string.Empty;

    [Description("The member's last name.")]
    [Phi]
    public string LastName { get; init; } = string.Empty;

    [Description("The member's date of birth.")]
    [Phi]
    public DateOnly DateOfBirth { get; init; }

    [Description("The member's first and last name separated by a space.")]
    [Phi]
    public string DisplayName { get; init; } = string.Empty;

    [Description("The member ID printed on the member's card.")]
    public string MemberNumber { get; init; } = string.Empty;

    [Description("The two-letter code of the state the coverage is treated as issued in, taken from the state of the enrollment's address.")]
    public string IssuanceState { get; init; } = string.Empty;

    [Description("The line of business of the enrollment's plan: Unknown, Commercial, Medicare, Medicaid or Exchange.")]
    public LineOfBusiness LineOfBusiness { get; init; }

    [Description("The ID of the plan the member is enrolled in.")]
    public string PlanId { get; init; } = string.Empty;

    [Description("The name of the plan the member is enrolled in.")]
    public string PlanName { get; init; } = string.Empty;

    [Description("The date the enrollment's coverage starts.")]
    public DateOnly EffectiveDate { get; init; }

    [Description("The date the enrollment's coverage ends; null if it has no end date.")]
    public DateOnly? TerminationDate { get; init; }
}
