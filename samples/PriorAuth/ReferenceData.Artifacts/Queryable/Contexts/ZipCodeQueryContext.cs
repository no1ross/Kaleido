using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Queryable.Contexts;

[QueryContext(
    Name = "zipcodes",
    DisplayName = "Zip Codes",
    Version = "1.0.0",
    Source = "Prior Authorization Reference Data",
    AllowDirectQuery = true)]
[Pageable(
    DefaultSize = 25,
    MaxSize = 250)]
public sealed class ZipCodeQueryContext
{
    [Key]
    [Searchable(
        Priority = 1,
        MatchMode = MatchMode.Exact)]
    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Sortable]
    public string PostalCode { get; init; } = string.Empty;

    [Searchable(
        Priority = 2,
        MatchMode = MatchMode.Exact)]
    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Sortable]
    public string StateCode { get; init; } = string.Empty;

    [Searchable(
        Priority = 3,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    public string City { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals)]
    [Sortable]
    public bool IsActive { get; init; }
}
