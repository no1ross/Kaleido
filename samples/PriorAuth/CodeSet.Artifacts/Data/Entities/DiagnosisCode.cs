namespace Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data.Entities;

public sealed class DiagnosisCode
{
    public Guid DiagnosisCodeId { get; set; }

    public string CodeValue { get; set; } = string.Empty;

    public DiagnosisCodeSystem CodeSystem { get; set; }

    public string ShortDescription { get; set; } = string.Empty;

    public string? LongDescription { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateOnly? TerminationDate { get; set; }
}
