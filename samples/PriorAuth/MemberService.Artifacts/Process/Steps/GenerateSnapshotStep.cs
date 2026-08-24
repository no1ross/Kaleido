using Kaleido.Process.Attributes;

namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts.Process.Steps;

[ProcessStep(
    Name = "GenerateSnapshot",
    DisplayName = "Members - Generate Snapshot",
    Description = "Generates a member snapshot for the current process context.",
    Version = "1.0.0")]
public sealed record GenerateSnapshotStep
{
    public required Guid MemberId { get; init; }

    public required Guid MemberEnrollmentId { get; init; }
}
