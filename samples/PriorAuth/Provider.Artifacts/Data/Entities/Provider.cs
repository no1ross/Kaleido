using Kaleido.Samples.PriorAuth.Provider;

namespace Kaleido.Samples.PriorAuth.Provider.Data.Entities;

public sealed class ProviderInfo
{
    public Guid ProviderId { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public ProviderType ProviderType { get; set; }

    public string? DoingBusinessAsName { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public ICollection<ProviderIdentifier> Identifiers { get; set; } = new List<ProviderIdentifier>();

    public ICollection<ProviderLocation> Locations { get; set; } = new List<ProviderLocation>();
}
