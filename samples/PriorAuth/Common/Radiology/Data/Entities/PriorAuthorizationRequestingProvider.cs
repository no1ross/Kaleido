using Kaleido.Samples.PriorAuth.Audit;

namespace Kaleido.Samples.PriorAuth.Radiology.Data.Entities;

public sealed class PriorAuthorizationRequestingProvider : IAuditable
{
    public Guid PriorAuthorizationId { get; set; }

    public Guid ProviderId { get; set; }

    public Guid? ProviderLocationId { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string? LocationName { get; set; }

    /// <summary>Whether the provider is in the member's network, from the search result chosen.</summary>
    public bool? IsInNetwork { get; set; }

    public PriorAuthorization PriorAuthorization { get; set; } = null!;

    /// <inheritdoc />
    public string CreatedBy { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? CreatedVia { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedUtc { get; set; }

    /// <inheritdoc />
    public string UpdatedBy { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? UpdatedVia { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedUtc { get; set; }
}
