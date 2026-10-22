using Kaleido.Samples.PriorAuth.Audit;

namespace Kaleido.Samples.PriorAuth.Intake.Data.Entities;

public sealed class IntakeSessionMember : IAuditable
{
    public Guid IntakeSessionId { get; set; }

    public Guid MemberId { get; set; }

    public Guid MemberEnrollmentId { get; set; }

    public string MemberNumber { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateOnly DateOfService { get; set; }

    public IntakeSession IntakeSession { get; set; } = null!;

    /// <inheritdoc />
    public string CreatedBy { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? CreatedVia { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedUtc { get; set; }

    /// <inheritdoc />
    public string UpdatedBy { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? UpdatedVia { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedUtc { get; set; }
}
