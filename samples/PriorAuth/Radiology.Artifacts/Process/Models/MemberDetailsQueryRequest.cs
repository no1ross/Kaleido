namespace Kaleido.Samples.PriorAuth.Radiology.Process.Models;

public sealed record MemberDetailsQueryParameters
{
    public Guid? MemberId { get; init; }

    public Guid? MemberEnrollmentId { get; init; }
}
