namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;

public sealed record MemberDetailsQueryParameters
{
    public Guid? MemberId { get; init; }

    public Guid? MemberEnrollmentId { get; init; }
}
