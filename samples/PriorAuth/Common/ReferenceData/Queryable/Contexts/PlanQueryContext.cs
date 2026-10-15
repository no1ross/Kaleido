using System.ComponentModel;
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
    [Description("The plan's identifier.")]
    public string PlanId { get; init; } = string.Empty;

    [Searchable(
        Priority = 2,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    [Description("The plan's name.")]
    public string PlanName { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Description("The plan's line of business (commercial, Medicare, Medicaid or exchange).")]
    public LineOfBusiness LineOfBusiness { get; init; }

    [Searchable(
        Priority = 3,
        MatchMode = MatchMode.Exact)]
    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Sortable]
    [Description("The two-letter code of the state the plan is offered in.")]
    public string StateCode { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals)]
    [Sortable]
    [Description("The date the plan takes effect.")]
    public DateOnly EffectiveDate { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.IsNull,
        FilterOperator.IsNotNull)]
    [Sortable]
    [Description("The date the plan ends, if it has an end date.")]
    public DateOnly? TerminationDate { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals)]
    [Sortable]
    [Description("Whether the plan is currently active.")]
    public bool IsActive { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.In)]
    [Description("The provider networks of the plan; a provider in one of them is in network.")]
    public Guid[] NetworkIds { get; init; } = [];
}
