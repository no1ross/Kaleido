namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts.Process.Responses;

public sealed record GenerateSnapshotResponse
{
    public required Guid MemberSnapshotId { get; init; }
}
