namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;

public sealed record MemberDetailsQueryRequest
{
    public MemberDetailsQueryParameters Parameters { get; init; } = new();
}

public sealed record MemberDetailsQueryParameters
{
    public Guid? MemberId { get; init; }

    public Guid? MemberEnrollmentId { get; init; }
}
