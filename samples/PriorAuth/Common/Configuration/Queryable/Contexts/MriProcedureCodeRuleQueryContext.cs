using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;

// Service-to-service only: queried by Radiology.
public sealed class MriProcedureCodeRuleQueryContext : IQueryContext
{
    [Key]
    [Description("The unique identifier of the MRI procedure code rule.")]
    public Guid MriProcedureCodeRuleId { get; init; }

    [Filterable(FilterOperator.Equals)]
    [Description("The code system of the procedure code the requester selected.")]
    public ProcedureCodeSystem SelectedCodeSystem { get; init; }

    [Searchable(Priority = 1, MatchMode = MatchMode.Exact)]
    [Filterable(FilterOperator.Equals)]
    [Description("The procedure code the requester selected, which this rule refines.")]
    public string SelectedCodeValue { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals)]
    [Description("The imaging modality the rule applies to, such as MRI.")]
    public ProcedureModality Modality { get; init; }

    [Filterable(FilterOperator.Equals)]
    [Description("The body part to be imaged that this rule matches.")]
    public MriBodyPart BodyPart { get; init; }

    [Filterable(FilterOperator.Equals)]
    [Description("The side of the body (none, left, right or bilateral) that this rule matches.")]
    public Laterality Laterality { get; init; }

    [Filterable(FilterOperator.Equals)]
    [Description("The contrast option (without, with, or with and without contrast) that this rule matches.")]
    public ContrastOption Contrast { get; init; }

    [Filterable(FilterOperator.Equals)]
    [Description("The code system of the procedure code this rule resolves to.")]
    public ProcedureCodeSystem ResolvedCodeSystem { get; init; }

    [Searchable(Priority = 2, MatchMode = MatchMode.Exact)]
    [Filterable(FilterOperator.Equals)]
    [Description("The procedure code to request once the selected code, body part, laterality and contrast are matched.")]
    public string ResolvedCodeValue { get; init; } = string.Empty;
}
