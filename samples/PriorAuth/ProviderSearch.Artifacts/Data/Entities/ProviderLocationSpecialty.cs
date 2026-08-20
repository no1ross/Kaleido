namespace Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data.Entities;

public sealed class ProviderLocationSpecialty
{
    public Guid ProviderLocationId { get; set; }

    public Guid MedicalSpecialtyId { get; set; }

    public bool IsPrimary { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public ProviderLocation ProviderLocation { get; set; } = null!;
}
