using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Queryable.Contexts;

public sealed class ZipCodeQueryContext : IQueryContext
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
    [Description("The ZIP code.")]
    public string PostalCode { get; init; } = string.Empty;

    [Searchable(
        Priority = 2,
        MatchMode = MatchMode.Exact)]
    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Sortable]
    [Description("The two-letter code of the state the ZIP code is in.")]
    public string StateCode { get; init; } = string.Empty;

    [Searchable(
        Priority = 3,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    [Description("The city of the ZIP code.")]
    public string City { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals)]
    [Sortable]
    [Description("Whether the ZIP code is currently active.")]
    public bool IsActive { get; init; }
}
