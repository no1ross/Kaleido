using Kaleido.Samples.PriorAuth.Member;

namespace Kaleido.Samples.PriorAuth.Intake.Data.Entities;

public sealed class PriorAuthorizationMember
{
    public Guid PriorAuthorizationId { get; set; }

    public Guid MemberId { get; set; }

    public Guid MemberEnrollmentId { get; set; }

    public string MemberNumber { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PlanId { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    public LineOfBusiness LineOfBusiness { get; set; }

    public PriorAuthorization PriorAuthorization { get; set; } = null!;
}
