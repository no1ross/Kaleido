using System.ComponentModel;

namespace Kaleido.Samples.PriorAuth.Provider.Queryable.ViewSources.Views;

public sealed record ProviderLocationSearchView
{
    [Description("The unique ID of the provider location: one practice or facility site of a provider.")]
    public Guid ProviderLocationId { get; init; }

    [Description("The unique ID of the provider (practice or organization) this location belongs to.")]
    public Guid ProviderId { get; init; }

    [Description("The provider's name.")]
    public string ProviderName { get; init; } = string.Empty;

    [Description("The name of this location.")]
    public string LocationName { get; init; } = string.Empty;

    [Description("The city the location is in.")]
    public string City { get; init; } = string.Empty;

    [Description("The two-letter code of the state the location is in.")]
    public string StateCode { get; init; } = string.Empty;

    [Description("The ZIP code of the location.")]
    public string PostalCode { get; init; } = string.Empty;

    [Description("The location's phone number; null if not on file.")]
    public string? PhoneNumber { get; init; }

    [Description("The provider's primary Taxpayer Identification Number (TIN); null if none is on file.")]
    public string? PrimaryTin { get; init; }

    [Description("The provider's primary National Provider Identifier (NPI); null if none is on file.")]
    public string? PrimaryNpi { get; init; }

    [Description("The ID of the location's primary medical specialty; null if it has none.")]
    public Guid? PrimaryMedicalSpecialtyId { get; init; }

    [Description("The name of the location's primary medical specialty; null if it has none.")]
    public string? PrimaryMedicalSpecialtyName { get; init; }

    [Description("The code of the location's primary medical specialty; null if it has none.")]
    public string? PrimaryMedicalSpecialtyCode { get; init; }

    [Description("The IDs of the networks the location participates in; this view does not populate it and always returns an empty list.")]
    public IReadOnlyCollection<Guid> NetworkIds { get; init; } = [];

    [Description("The IDs of all the location's medical specialties; this view does not populate it and always returns an empty list.")]
    public IReadOnlyCollection<Guid> MedicalSpecialtyIds { get; init; } = [];
}
