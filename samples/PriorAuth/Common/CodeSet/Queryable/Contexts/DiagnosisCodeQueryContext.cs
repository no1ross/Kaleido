using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Samples.PriorAuth.CodeSet.Queryable.Contexts;

public sealed class DiagnosisCodeQueryContext : IQueryContext
{
    [Key]
    [Description("The unique identifier of the diagnosis code record.")]
    public Guid DiagnosisCodeId { get; init; }

    [Searchable(
        Priority = 1,
        MatchMode = MatchMode.Exact)]
    [Sortable]
    [Description("The diagnosis code itself, as written in its code system (e.g. an ICD-10 code).")]
    public string CodeValue { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Sortable]
    [Description("The code system the diagnosis code belongs to, such as ICD-10.")]
    public DiagnosisCodeSystem CodeSystem { get; init; }

    [Searchable(
        Priority = 2,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    [Description("A short, readable name for the diagnosis.")]
    public string ShortDescription { get; init; } = string.Empty;

    [Searchable(
        Priority = 3,
        MatchMode = MatchMode.Contains)]
    [Description("The full description of the diagnosis, when one is available.")]
    public string? LongDescription { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.GreaterThan,
        FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan,
        FilterOperator.LessThanOrEqual)]
    [Sortable]
    [Description("The date from which the diagnosis code is valid.")]
    public DateOnly EffectiveDate { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.IsNull,
        FilterOperator.IsNotNull,
        FilterOperator.GreaterThan,
        FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan,
        FilterOperator.LessThanOrEqual)]
    [Sortable]
    [Description("The date after which the diagnosis code is no longer valid; empty while it is still in use.")]
    public DateOnly? TerminationDate { get; init; }
}
