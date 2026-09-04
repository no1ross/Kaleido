namespace Kaleido.Samples.PriorAuth.CodeSet.Data.Entities;

public sealed class ProcedureCodeGrouperAssignment
{
    public Guid ProcedureCodeId { get; set; }

    public Guid CodeGrouperId { get; set; }

    public int? Rank { get; set; }

    public bool IsPrimary { get; set; }

    public ProcedureCode ProcedureCode { get; set; } = null!;

    public CodeGrouper CodeGrouper { get; set; } = null!;
}
