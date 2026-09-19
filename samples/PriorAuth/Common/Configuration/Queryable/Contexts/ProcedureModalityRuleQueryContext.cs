using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Metadata;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;

[QueryContext(
    Name = "procedure-modality-rules",
    DisplayName = "Procedure Modality Rules",
    Version = "1.0.0",
    Source = "Prior Authorization Configuration",
    Kind = QueryContextKind.Direct)]
public sealed class ProcedureModalityRuleQueryContext
{
    [Key]
    public Guid ProcedureModalityRuleId { get; init; }

    [Filterable(FilterOperator.Equals)]
    public ProcedureCodeSystem CodeSystem { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThanOrEqual)]
    public int CodeRangeStart { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThanOrEqual)]
    public int CodeRangeEnd { get; init; }

    [Filterable(FilterOperator.Equals)]
    public ProcedureModality Modality { get; init; }

    [Searchable(Priority = 1, MatchMode = MatchMode.Contains)]
    public string Name { get; init; } = string.Empty;
}
