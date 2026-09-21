namespace Kaleido.Samples.PriorAuth.Intake.Data.Entities;

public sealed class IntakeSessionMember
{
    public Guid IntakeSessionId { get; set; }

    public Guid MemberId { get; set; }

    public Guid MemberEnrollmentId { get; set; }

    public string MemberNumber { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateOnly DateOfService { get; set; }

    public IntakeSession IntakeSession { get; set; } = null!;
}
