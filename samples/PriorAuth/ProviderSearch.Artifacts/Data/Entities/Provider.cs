namespace Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data.Entities;

public sealed class Provider
{
    public Guid ProviderId { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string? DoingBusinessAsName { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public ICollection<ProviderIdentifier> Identifiers { get; set; } = new List<ProviderIdentifier>();

    public ICollection<ProviderLocation> Locations { get; set; } = new List<ProviderLocation>();
}
