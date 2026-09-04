namespace Kaleido.Samples.PriorAuth.Intake.Data.Entities;

public sealed class PriorAuthorizationRequestingProvider
{
    public Guid PriorAuthorizationId { get; set; }

    public Guid ProviderId { get; set; }

    public Guid? ProviderLocationId { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string? LocationName { get; set; }

    public PriorAuthorization PriorAuthorization { get; set; } = null!;
}
