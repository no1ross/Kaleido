using Kaleido.Samples.PriorAuth.CodeSet;

namespace Kaleido.Samples.PriorAuth.Intake.Data.Entities;

public sealed class IntakeSessionProcedure
{
    public Guid IntakeSessionId { get; set; }

    public string CodeValue { get; set; } = string.Empty;

    public ProcedureCodeSystem CodeSystem { get; set; }

    public string ResolvedProcessorName { get; set; } = string.Empty;

    public IntakeSession IntakeSession { get; set; } = null!;
}
