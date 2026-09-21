namespace Kaleido.Samples.PriorAuth.CodeSet.Data.Entities;

public sealed class CodeGrouper
{
    public Guid CodeGrouperId { get; set; }

    public string Name { get; set; } = string.Empty;

    public GroupingType GroupingType { get; set; }

    public string? Description { get; set; }

    public string? Source { get; set; }

    public string Version { get; set; } = string.Empty;

    public DateOnly EffectiveDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public ICollection<ProcedureCodeGrouperAssignment> ProcedureCodeAssignments { get; set; } = new List<ProcedureCodeGrouperAssignment>();
}
