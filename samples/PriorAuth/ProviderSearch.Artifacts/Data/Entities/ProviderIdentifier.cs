namespace Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data.Entities;

public sealed class ProviderIdentifier
{
    public Guid ProviderIdentifierId { get; set; }

    public Guid ProviderId { get; set; }

    public ProviderIdentifierType IdentifierType { get; set; }

    public string IdentifierValue { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public Provider Provider { get; set; } = null!;
}
