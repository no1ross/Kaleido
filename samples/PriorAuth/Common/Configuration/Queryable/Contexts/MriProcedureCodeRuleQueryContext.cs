using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;

// Service-to-service only: queried by Radiology.
public sealed class MriProcedureCodeRuleQueryContext : IQueryContext
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
