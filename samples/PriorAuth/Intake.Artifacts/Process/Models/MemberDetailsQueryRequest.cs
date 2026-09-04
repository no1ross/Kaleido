namespace Kaleido.Samples.PriorAuth.Intake.Process.Models;

public sealed record MemberDetailsQueryParameters
{
    public Guid? MemberId { get; init; }

    public Guid? MemberEnrollmentId { get; init; }
}
