using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;

namespace Kaleido.Samples.PriorAuth.Provider.Queryable.Contexts;

// Service-to-service only: no direct consumer use.
public sealed class ProviderLocationQueryContext : IQueryContext
{
    [Key]
    [Description("The provider location: one practice or facility site of a provider.")]
    public Guid ProviderLocationId { get; init; }

    [Description("The provider (practice or organization) this location belongs to.")]
    public Guid ProviderId { get; init; }

    [Searchable(Priority = 1, MatchMode = MatchMode.Contains)]
    [Sortable]
    [Description("The provider's name.")]
    public string ProviderName { get; init; } = string.Empty;

    [Searchable(Priority = 2, MatchMode = MatchMode.Contains)]
    [Sortable]
    [Description("The name of this location.")]
    public string LocationName { get; init; } = string.Empty;

    [Searchable(Priority = 3, MatchMode = MatchMode.Exact)]
    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.In)]
    [Description("The two-letter code of the state the location is in.")]
    public string StateCode { get; init; } = string.Empty;

    [Searchable(Priority = 4, MatchMode = MatchMode.Exact)]
    [Sortable]
    [Description("The ZIP code of the location.")]
    public string PostalCode { get; init; } = string.Empty;

    [Sortable]
    [Description("The city the location is in.")]
    public string City { get; init; } = string.Empty;

    [Description("The location's phone number.")]
    public string? PhoneNumber { get; init; }

    [Description("The provider's primary Taxpayer Identification Number (TIN).")]
    public string? PrimaryTin { get; init; }

    [Description("The provider's primary National Provider Identifier (NPI).")]
    public string? PrimaryNpi { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    [Description("The location's primary medical specialty.")]
    public Guid? PrimaryMedicalSpecialtyId { get; init; }

    [Searchable(Priority = 5, MatchMode = MatchMode.Contains)]
    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    [Description("The name of the location's primary medical specialty.")]
    public string? PrimaryMedicalSpecialtyName { get; init; }

    [Searchable(Priority = 6, MatchMode = MatchMode.Exact)]
    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    [Description("The code of the location's primary medical specialty.")]
    public string? PrimaryMedicalSpecialtyCode { get; init; }

    [Description("The kind of provider: a requesting provider or a servicing facility.")]
    public ProviderType ProviderType { get; init; }

    [Description("Whether the location is currently active.")]
    public bool IsActive { get; init; }
}
