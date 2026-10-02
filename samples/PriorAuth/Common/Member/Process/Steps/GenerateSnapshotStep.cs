using Kaleido;
using Kaleido.Process;

namespace Kaleido.Samples.PriorAuth.Member.Process.Steps;

// Internal step — service-to-service callers only (radiology snapshot fan-out);
// not visible to or executable by user personas.
[KaleidoAuthorization(Roles = "internal")]
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
