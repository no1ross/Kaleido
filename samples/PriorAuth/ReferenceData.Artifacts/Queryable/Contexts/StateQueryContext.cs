using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Metadata;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Queryable.Contexts;

[QueryContext(
    Name = "states",
    DisplayName = "States",
    Version = "1.0.0",
    Source = "Prior Authorization Reference Data",
    Kind = QueryContextKind.Direct)]
[Pageable(
    DefaultSize = 25,
    MaxSize = 100)]
public sealed class StateQueryContext
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
