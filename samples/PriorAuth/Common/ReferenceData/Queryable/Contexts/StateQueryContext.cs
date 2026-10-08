using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Queryable.Contexts;

public sealed class StateQueryContext : IQueryContext
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
    public string StateCode { get; init; } = string.Empty;

    [Searchable(
        Priority = 2,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    public string Name { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals)]
    [Sortable]
    public bool IsActive { get; init; }
}
