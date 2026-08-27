using Kaleido.Samples.PriorAuth.MemberService.Artifacts;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;

public sealed record MemberDetailsRecord
{
    public Guid MemberId { get; init; }

    public Guid MemberEnrollmentId { get; init; }

    public string MemberNumber { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string PlanId { get; init; } = string.Empty;

    public string PlanName { get; init; } = string.Empty;

    public LineOfBusiness LineOfBusiness { get; init; }

    public DateOnly EffectiveDate { get; init; }

    public DateOnly? TerminationDate { get; init; }
}
