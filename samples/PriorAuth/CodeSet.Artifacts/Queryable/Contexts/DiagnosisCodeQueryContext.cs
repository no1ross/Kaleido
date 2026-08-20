using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Queryable.Contexts;

[QueryContext(
    Name = "diagnosis-codes",
    DisplayName = "Diagnosis Codes",
    Version = "1.0.0",
    Source = "Prior Authorization Code Set",
    AllowDirectQuery = true)]
[Pageable(
    DefaultSize = 25,
    MaxSize = 100)]
public sealed class DiagnosisCodeQueryContext
{
    [Key]
    public Guid DiagnosisCodeId { get; init; }

    [Searchable(
        Priority = 1,
        MatchMode = MatchMode.Exact)]
    [Sortable]
    public string CodeValue { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Sortable]
    public DiagnosisCodeSystem CodeSystem { get; init; }

    [Searchable(
        Priority = 2,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    public string ShortDescription { get; init; } = string.Empty;

    [Searchable(
        Priority = 3,
        MatchMode = MatchMode.Contains)]
    public string? LongDescription { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.GreaterThan,
        FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan,
        FilterOperator.LessThanOrEqual)]
    [Sortable]
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
    public DateOnly? TerminationDate { get; init; }
}
