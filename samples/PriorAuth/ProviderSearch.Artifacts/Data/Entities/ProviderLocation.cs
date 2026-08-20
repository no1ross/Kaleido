namespace Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data.Entities;

public sealed class ProviderLocation
{
    public Guid ProviderLocationId { get; set; }

    public Guid ProviderId { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string StateCode { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public Provider Provider { get; set; } = null!;

    public ICollection<ProviderLocationNetwork> Networks { get; set; } = new List<ProviderLocationNetwork>();

    public ICollection<ProviderLocationSpecialty> Specialties { get; set; } = new List<ProviderLocationSpecialty>();
}
