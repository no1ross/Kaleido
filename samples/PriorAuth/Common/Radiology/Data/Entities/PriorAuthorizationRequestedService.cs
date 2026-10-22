using Kaleido.Samples.PriorAuth.Audit;

namespace Kaleido.Samples.PriorAuth.Radiology.Data.Entities;

public sealed class PriorAuthorizationRequestedService : IAuditable
{
    public Guid PriorAuthorizationRequestedServiceId { get; set; }

    public Guid PriorAuthorizationId { get; set; }

    public Guid? UserEnteredProcedureCodeId { get; set; }

    public string UserEnteredCodeValue { get; set; } = string.Empty;

    public ProcedureCodeSystem UserEnteredCodeSystem { get; set; }

    public Guid? ResolvedProcedureCodeId { get; set; }

    public string ResolvedCodeValue { get; set; } = string.Empty;

    public ProcedureCodeSystem ResolvedCodeSystem { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// What the caller stated about this service up front, by binding key (e.g. <c>Laterality</c> →
    /// <c>Left</c>), as JSON. Configured questions with the same binding key propose these values for
    /// this service; the caller confirms them.
    /// </summary>
    public string? StatedDetailsJson { get; set; }

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
