namespace Kaleido.Samples.PriorAuth.Member.Process.Responses;

public sealed record GenerateSnapshotResponse
{
    public required Guid MemberSnapshotId { get; init; }
}
