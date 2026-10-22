using Kaleido.Samples.PriorAuth.Audit;

namespace Kaleido.Samples.PriorAuth.Intake.Data.Entities;

public sealed class IntakeSessionProcedure : IAuditable
{
    public Guid IntakeSessionId { get; set; }

    public string CodeValue { get; set; } = string.Empty;

    public ProcedureCodeSystem CodeSystem { get; set; }

    public string Description { get; set; } = string.Empty;

    public string ResolvedProcessorName { get; set; } = string.Empty;

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
