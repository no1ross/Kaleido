namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data.Entities;

public sealed class MemberEnrollment
{
    public Guid MemberEnrollmentId { get; set; }

    public Guid MemberId { get; set; }

    public Guid MemberAddressId { get; set; }

    public string PlanId { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    public LineOfBusiness LineOfBusiness { get; set; }

    public RelationshipToSubscriber RelationshipToSubscriber { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public bool IsCurrent { get; set; }

    public MemberAddress Address { get; set; } = null!;

    public Member Member { get; set; } = null!;
}
