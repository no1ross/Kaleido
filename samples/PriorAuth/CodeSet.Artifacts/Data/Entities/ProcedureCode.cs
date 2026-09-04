namespace Kaleido.Samples.PriorAuth.CodeSet.Data.Entities;

public sealed class ProcedureCode
{
    public Guid ProcedureCodeId { get; set; }

    public string CodeValue { get; set; } = string.Empty;

    public ProcedureCodeSystem CodeSystem { get; set; }

    public string ShortDescription { get; set; } = string.Empty;

    public string? LongDescription { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public bool RequiresAuthorization { get; set; }

    public ICollection<ProcedureCodeSpecialtyAssignment> SpecialtyAssignments { get; set; } = new List<ProcedureCodeSpecialtyAssignment>();

    public ICollection<ProcedureCodeGrouperAssignment> GrouperAssignments { get; set; } = new List<ProcedureCodeGrouperAssignment>();
}
