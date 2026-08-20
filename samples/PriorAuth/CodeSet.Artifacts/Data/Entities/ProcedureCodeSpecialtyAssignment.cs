namespace Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data.Entities;

public sealed class ProcedureCodeSpecialtyAssignment
{
    public Guid ProcedureCodeId { get; set; }

    public Guid MedicalSpecialtyId { get; set; }

    public bool IsPrimary { get; set; }

    public ProcedureCode ProcedureCode { get; set; } = null!;

    public MedicalSpecialty MedicalSpecialty { get; set; } = null!;
}
