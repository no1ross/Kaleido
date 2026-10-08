using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Queryable.Contexts;

public sealed class PlanQueryContext : IQueryContext
{
    [Key]
    [Searchable(
        Priority = 1,
        MatchMode = MatchMode.Exact)]
    [Sortable]
    public string PlanId { get; init; } = string.Empty;

    [Searchable(
        Priority = 2,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    public string PlanName { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    public LineOfBusiness LineOfBusiness { get; init; }

    [Searchable(
        Priority = 3,
        MatchMode = MatchMode.Exact)]
    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Sortable]
    public string StateCode { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals)]
    [Sortable]
    public DateOnly EffectiveDate { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.IsNull,
        FilterOperator.IsNotNull)]
    [Sortable]
    public DateOnly? TerminationDate { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals)]
    [Sortable]
    public bool IsActive { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.In)]
    public Guid[] NetworkIds { get; init; } = [];
}
