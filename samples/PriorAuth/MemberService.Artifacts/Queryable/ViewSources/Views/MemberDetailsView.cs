namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts.Queryable.ViewSources.Views;

public sealed record MemberDetailsView
{
    public Guid MemberId { get; init; }

    public Guid MemberEnrollmentId { get; init; }

    public string MemberNumber { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public DateOnly DateOfBirth { get; init; }

    public MemberGender Gender { get; init; }

    public string? EmailAddress { get; init; }

    public string? PhoneNumber { get; init; }

    public string PlanId { get; init; } = string.Empty;

    public string PlanName { get; init; } = string.Empty;

    public LineOfBusiness LineOfBusiness { get; init; }

    public DateOnly EffectiveDate { get; init; }

    public DateOnly? TerminationDate { get; init; }

    public RelationshipToSubscriber RelationshipToSubscriber { get; init; }

    public string IssuanceState { get; init; } = string.Empty;

    public string AddressLine1 { get; init; } = string.Empty;

    public string? AddressLine2 { get; init; }

    public string City { get; init; } = string.Empty;

    public string AddressState { get; init; } = string.Empty;

    public string PostalCode { get; init; } = string.Empty;
}
