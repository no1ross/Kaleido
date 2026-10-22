using Kaleido.Samples.PriorAuth.Audit;

namespace Kaleido.Samples.PriorAuth.Radiology.Data.Entities;

public sealed class PriorAuthorizationMember : IAuditable
{
    public Guid PriorAuthorizationId { get; set; }

    public Guid MemberId { get; set; }

    public Guid MemberEnrollmentId { get; set; }

    public string MemberNumber { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PlanId { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    public LineOfBusiness LineOfBusiness { get; set; }

    public PriorAuthorization PriorAuthorization { get; set; } = null!;

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
