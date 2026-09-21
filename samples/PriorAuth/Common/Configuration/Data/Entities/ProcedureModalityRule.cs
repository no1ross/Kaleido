namespace Kaleido.Samples.PriorAuth.Configuration.Data.Entities;

public sealed class ProcedureModalityRule
{
    public Guid ProcedureModalityRuleId { get; set; }

    public ProcedureCodeSystem CodeSystem { get; set; }

    public int CodeRangeStart { get; set; }

    public int CodeRangeEnd { get; set; }

    public ProcedureModality Modality { get; set; }

    public string Name { get; set; } = string.Empty;
}
