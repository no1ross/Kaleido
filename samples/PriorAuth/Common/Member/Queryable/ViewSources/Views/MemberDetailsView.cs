using Kaleido.Samples.PriorAuth.Compliance;

namespace Kaleido.Samples.PriorAuth.Member.Queryable.ViewSources.Views;

public sealed record MemberDetailsView
{
    public Guid MemberId { get; init; }

    public Guid MemberEnrollmentId { get; init; }

    public string MemberNumber { get; init; } = string.Empty;

    [Phi]
    public string FirstName { get; init; } = string.Empty;

    [Phi]
    public string LastName { get; init; } = string.Empty;

    [Phi]
    public string DisplayName { get; init; } = string.Empty;

    [Phi]
    public DateOnly DateOfBirth { get; init; }

    public MemberGender Gender { get; init; }

    [Pii]
    public string? EmailAddress { get; init; }

    [Pii]
    public string? PhoneNumber { get; init; }

    public string PlanId { get; init; } = string.Empty;

    public string PlanName { get; init; } = string.Empty;

    public LineOfBusiness LineOfBusiness { get; init; }

    public DateOnly EffectiveDate { get; init; }

    public DateOnly? TerminationDate { get; init; }

    public RelationshipToSubscriber RelationshipToSubscriber { get; init; }

    public string IssuanceState { get; init; } = string.Empty;

    [Pii]
    public string AddressLine1 { get; init; } = string.Empty;

    [Pii]
    public string? AddressLine2 { get; init; }

    [Pii]
    public string City { get; init; } = string.Empty;

    public string AddressState { get; init; } = string.Empty;

    [Pii]
    public string PostalCode { get; init; } = string.Empty;
}
