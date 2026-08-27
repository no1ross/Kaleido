using Kaleido.Samples.PriorAuth.CodeSet.Artifacts;

namespace Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Entities;

public sealed class MriProcedureCodeRule
{
    public Guid MriProcedureCodeRuleId { get; set; }

    public ProcedureCodeSystem SelectedCodeSystem { get; set; }

    public string SelectedCodeValue { get; set; } = string.Empty;

    public MriBodyPart BodyPart { get; set; }

    public Laterality Laterality { get; set; }

    public ContrastOption Contrast { get; set; }

    public ProcedureCodeSystem ResolvedCodeSystem { get; set; }

    public string ResolvedCodeValue { get; set; } = string.Empty;
}
