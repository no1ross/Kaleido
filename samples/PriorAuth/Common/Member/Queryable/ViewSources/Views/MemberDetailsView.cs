using System.ComponentModel;
using Kaleido.Samples.PriorAuth.Compliance;

namespace Kaleido.Samples.PriorAuth.Member.Queryable.ViewSources.Views;

public sealed record MemberDetailsView
{
    [Description("The unique ID of the member.")]
    public Guid MemberId { get; init; }

    [Description("The unique ID of the member's enrollment (plan coverage) this row describes.")]
    public Guid MemberEnrollmentId { get; init; }

    [Description("The member ID printed on the member's card.")]
    public string MemberNumber { get; init; } = string.Empty;

    [Description("The member's first name.")]
    [Phi]
    public string FirstName { get; init; } = string.Empty;

    [Description("The member's last name.")]
    [Phi]
    public string LastName { get; init; } = string.Empty;

    [Description("The member's first and last name separated by a space.")]
    [Phi]
    public string DisplayName { get; init; } = string.Empty;

    [Description("The member's date of birth.")]
    [Phi]
    public DateOnly DateOfBirth { get; init; }

    [Description("The member's gender: Unknown, Female, Male or NonBinary.")]
    public MemberGender Gender { get; init; }

    [Description("The member's email address; null if not on file.")]
    [Pii]
    public string? EmailAddress { get; init; }

    [Description("The member's phone number; null if not on file.")]
    [Pii]
    public string? PhoneNumber { get; init; }

    [Description("The ID of the plan the member is enrolled in.")]
    public string PlanId { get; init; } = string.Empty;

    [Description("The name of the plan the member is enrolled in.")]
    public string PlanName { get; init; } = string.Empty;

    [Description("The line of business of the enrollment's plan: Unknown, Commercial, Medicare, Medicaid or Exchange.")]
    public LineOfBusiness LineOfBusiness { get; init; }

    [Description("The date the enrollment's coverage starts.")]
    public DateOnly EffectiveDate { get; init; }

    [Description("The date the enrollment's coverage ends; null if it has no end date.")]
    public DateOnly? TerminationDate { get; init; }

    [Description("The member's relationship to the plan subscriber: Unknown, Subscriber, Spouse, Child or OtherDependent.")]
    public RelationshipToSubscriber RelationshipToSubscriber { get; init; }

    [Description("The two-letter code of the state the coverage is treated as issued in, taken from the state of the enrollment's address.")]
    public string IssuanceState { get; init; } = string.Empty;

    [Description("The first line of the enrollment's mailing address.")]
    [Pii]
    public string AddressLine1 { get; init; } = string.Empty;

    [Description("The second line of the enrollment's mailing address; null if none.")]
    [Pii]
    public string? AddressLine2 { get; init; }

    [Description("The city of the enrollment's mailing address.")]
    [Pii]
    public string City { get; init; } = string.Empty;

    [Description("The two-letter state code of the enrollment's mailing address.")]
    public string AddressState { get; init; } = string.Empty;

    [Description("The postal (ZIP) code of the enrollment's mailing address.")]
    [Pii]
    public string PostalCode { get; init; } = string.Empty;
}
