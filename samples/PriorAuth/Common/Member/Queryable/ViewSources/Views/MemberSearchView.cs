using Kaleido.Samples.PriorAuth.Member;

namespace Kaleido.Samples.PriorAuth.Member.Queryable.ViewSources.Views;

public sealed record MemberSearchView
{
    public Guid MemberId { get; init; }

    public Guid MemberEnrollmentId { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public DateOnly DateOfBirth { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string MemberNumber { get; init; } = string.Empty;

    public string IssuanceState { get; init; } = string.Empty;

    public LineOfBusiness LineOfBusiness { get; init; }

    public string PlanId { get; init; } = string.Empty;

    public string PlanName { get; init; } = string.Empty;

    public DateOnly EffectiveDate { get; init; }

    public DateOnly? TerminationDate { get; init; }
}
