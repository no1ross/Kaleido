using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using Kaleido.Samples.PriorAuth.CodeSet.Artifacts;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Configuration.Artifacts.Queryable.Contexts;

[QueryContext(
    Name = "mri-procedure-code-rules",
    DisplayName = "MRI Procedure Code Rules",
    Version = "1.0.0",
    Source = "Prior Authorization Configuration",
    AllowDirectQuery = true)]
public sealed class MriProcedureCodeRuleQueryContext
{
    [Key]
    public Guid MriProcedureCodeRuleId { get; init; }

    [Filterable(FilterOperator.Equals)]
    public ProcedureCodeSystem SelectedCodeSystem { get; init; }

    [Searchable(Priority = 1, MatchMode = MatchMode.Exact)]
    [Filterable(FilterOperator.Equals)]
    public string SelectedCodeValue { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals)]
    public ProcedureModality Modality { get; init; }

    [Filterable(FilterOperator.Equals)]
    public MriBodyPart BodyPart { get; init; }

    [Filterable(FilterOperator.Equals)]
    public Laterality Laterality { get; init; }

    [Filterable(FilterOperator.Equals)]
    public ContrastOption Contrast { get; init; }

    [Filterable(FilterOperator.Equals)]
    public ProcedureCodeSystem ResolvedCodeSystem { get; init; }

    [Searchable(Priority = 2, MatchMode = MatchMode.Exact)]
    [Filterable(FilterOperator.Equals)]
    public string ResolvedCodeValue { get; init; } = string.Empty;
}
