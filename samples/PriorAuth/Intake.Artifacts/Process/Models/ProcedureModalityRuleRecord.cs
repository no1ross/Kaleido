using Kaleido.Samples.PriorAuth.Configuration.Artifacts;
using Kaleido.Samples.PriorAuth.CodeSet.Artifacts;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;

public sealed record ProcedureModalityRuleRecord
{
    public Guid ProcedureModalityRuleId { get; init; }

    public ProcedureCodeSystem CodeSystem { get; init; }

    public int CodeRangeStart { get; init; }

    public int CodeRangeEnd { get; init; }

    public ProcedureModality Modality { get; init; }

    public string Name { get; init; } = string.Empty;
}
