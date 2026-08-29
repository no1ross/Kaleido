namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;

public sealed record RequestingProviderSearchRecord
{
    public Guid ProviderLocationId { get; init; }

    public Guid ProviderId { get; init; }

    public string ProviderName { get; init; } = string.Empty;

    public string LocationName { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string StateCode { get; init; } = string.Empty;

    public string PostalCode { get; init; } = string.Empty;

    public string? PhoneNumber { get; init; }

    public string? PrimaryTin { get; init; }

    public string? PrimaryNpi { get; init; }

    public Guid? PrimaryMedicalSpecialtyId { get; init; }

    public string? PrimaryMedicalSpecialtyName { get; init; }

    public string? PrimaryMedicalSpecialtyCode { get; init; }

    public bool IsInNetwork { get; init; }
}
