using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;

// Service-to-service only: queried by Intake and Radiology.
public sealed class ProcedureModalityRuleQueryContext : IQueryContext
{
    [Key]
    [Description("The unique identifier of the procedure modality rule.")]
    public Guid ProcedureModalityRuleId { get; init; }

    [Filterable(FilterOperator.Equals)]
    [Description("The code system of the procedure codes the rule covers, such as CPT.")]
    public ProcedureCodeSystem CodeSystem { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThanOrEqual)]
    [Description("The lowest numeric procedure code the rule covers (inclusive).")]
    public int CodeRangeStart { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThanOrEqual)]
    [Description("The highest numeric procedure code the rule covers (inclusive).")]
    public int CodeRangeEnd { get; init; }

    [Filterable(FilterOperator.Equals)]
    [Description("The imaging modality, such as MRI or CT, assigned to procedure codes in the range.")]
    public ProcedureModality Modality { get; init; }

    [Searchable(Priority = 1, MatchMode = MatchMode.Contains)]
    [Description("A readable name for the rule.")]
    public string Name { get; init; } = string.Empty;
}
