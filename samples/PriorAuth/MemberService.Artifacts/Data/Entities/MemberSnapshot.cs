namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data.Entities;

public sealed class MemberSnapshot
{
    public Guid MemberSnapshotId { get; set; }

    public Guid MemberId { get; set; }

    public Guid MemberEnrollmentId { get; set; }

    public int SchemaVersion { get; set; }

    public DateTimeOffset CapturedUtc { get; set; }

    public string SnapshotJson { get; set; } = string.Empty;
}
