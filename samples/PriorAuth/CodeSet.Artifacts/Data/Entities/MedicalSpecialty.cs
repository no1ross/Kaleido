namespace Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data.Entities;

public sealed class MedicalSpecialty
{
    public Guid MedicalSpecialtyId { get; set; }

    public string SpecialtyCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<ProcedureCodeSpecialtyAssignment> ProcedureCodeAssignments { get; set; } = new List<ProcedureCodeSpecialtyAssignment>();
}
