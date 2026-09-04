using Kaleido.Samples.PriorAuth.Configuration;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Models;

public sealed record ProcedureModalityRuleRecord
{
    public Guid ProcedureModalityRuleId { get; init; }

    public ProcedureCodeSystem CodeSystem { get; init; }

    public int CodeRangeStart { get; init; }

    public int CodeRangeEnd { get; init; }

    public ProcedureModality Modality { get; init; }

    public string Name { get; init; } = string.Empty;
}
