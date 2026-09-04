using Kaleido.Samples.PriorAuth.CodeSet;
using Kaleido.Samples.PriorAuth.Radiology;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Models;

public sealed record MriProcedureCodeRuleRecord
{
    public Guid MriProcedureCodeRuleId { get; init; }

    public ProcedureCodeSystem SelectedCodeSystem { get; init; }

    public string SelectedCodeValue { get; init; } = string.Empty;

    public MriBodyPart BodyPart { get; init; }

    public Laterality Laterality { get; init; }

    public ContrastOption Contrast { get; init; }

    public ProcedureCodeSystem ResolvedCodeSystem { get; init; }

    public string ResolvedCodeValue { get; init; } = string.Empty;
}
