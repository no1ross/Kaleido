using Kaleido.Samples.PriorAuth.CodeSet;
using Kaleido.Samples.PriorAuth.CodeSet.Data.Entities;

namespace Kaleido.Samples.PriorAuth.Seeder.CodeSet;

internal sealed class CodeSetSeedAssets
{
    public required List<ProcedureCode> ProcedureCodes { get; init; }

    public required List<DiagnosisCode> DiagnosisCodes { get; init; }

    public required List<MedicalSpecialty> Specialties { get; init; }

    public required List<CodeGrouper> Groupers { get; init; }

    public required List<ProcedureCodeSpecialtyAssignmentAsset> ProcedureCodeSpecialtyAssignments { get; init; }

    public required List<ProcedureCodeGrouperAssignmentAsset> ProcedureCodeGrouperAssignments { get; init; }
}

internal sealed class ProcedureCodeSpecialtyAssignmentAsset
{
    public required string CodeValue { get; init; }

    public required ProcedureCodeSystem CodeSystem { get; init; }

    public required string SpecialtyCode { get; init; }

    public bool IsPrimary { get; init; }
}

internal sealed class ProcedureCodeGrouperAssignmentAsset
{
    public required string CodeValue { get; init; }

    public required ProcedureCodeSystem CodeSystem { get; init; }

    public required string GrouperName { get; init; }

    public int? Rank { get; init; }

    public bool IsPrimary { get; init; }
}
