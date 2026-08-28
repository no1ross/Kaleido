using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Queryable.Contexts;

[QueryContext(
    Name = "provider-locations",
    DisplayName = "Provider Locations",
    Version = "1.0.0",
    Source = "Prior Authorization Provider Search")]
[Pageable(DefaultSize = 25, MaxSize = 250)]
public sealed class ProviderLocationQueryContext
{
    [Key]
    public Guid ProviderLocationId { get; init; }

    public Guid ProviderId { get; init; }

    [Searchable(Priority = 1, MatchMode = MatchMode.Contains)]
    [Sortable]
    public string ProviderName { get; init; } = string.Empty;

    [Searchable(Priority = 2, MatchMode = MatchMode.Contains)]
    [Sortable]
    public string LocationName { get; init; } = string.Empty;

    [Searchable(Priority = 3, MatchMode = MatchMode.Exact)]
    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.In)]
    public string StateCode { get; init; } = string.Empty;

    [Searchable(Priority = 4, MatchMode = MatchMode.Exact)]
    [Sortable]
    public string PostalCode { get; init; } = string.Empty;

    [Sortable]
    public string City { get; init; } = string.Empty;

    public string? PhoneNumber { get; init; }

    public string? PrimaryTin { get; init; }

    public string? PrimaryNpi { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    public Guid? PrimaryMedicalSpecialtyId { get; init; }

    [Searchable(Priority = 5, MatchMode = MatchMode.Contains)]
    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    public string? PrimaryMedicalSpecialtyName { get; init; }

    [Searchable(Priority = 6, MatchMode = MatchMode.Exact)]
    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    public string? PrimaryMedicalSpecialtyCode { get; init; }

    public bool IsActive { get; init; }
}
