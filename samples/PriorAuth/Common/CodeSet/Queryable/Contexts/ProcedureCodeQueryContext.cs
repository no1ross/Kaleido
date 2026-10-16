using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Samples.PriorAuth.CodeSet.Queryable.Contexts;

public sealed class ProcedureCodeQueryContext : IQueryContext
{
    [Key]
    [Description("The unique identifier of the procedure code record.")]
    public Guid ProcedureCodeId { get; init; }

    [Searchable(
        Priority = 1,
        MatchMode = MatchMode.Exact)]
    [Sortable]
    [Description("The procedure code itself, as written in its code system (e.g. a CPT or HCPCS code).")]
    public string CodeValue { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Sortable]
    [Description("The code system the procedure code belongs to, such as CPT or HCPCS.")]
    public ProcedureCodeSystem CodeSystem { get; init; }

    [Searchable(
        Priority = 2,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    [Description("A short, readable name for the procedure.")]
    public string ShortDescription { get; init; } = string.Empty;

    [Searchable(
        Priority = 3,
        MatchMode = MatchMode.Contains)]
    [Description("The full description of the procedure, when one is available.")]
    public string? LongDescription { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals)]
    [Sortable]
    [Description("Whether the procedure needs prior authorization before it is rendered.")]
    public bool RequiresAuthorization { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.GreaterThan,
        FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan,
        FilterOperator.LessThanOrEqual)]
    [Sortable]
    [Description("The date from which the procedure code is valid.")]
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
    [Description("The date after which the procedure code is no longer valid; empty while it is still in use.")]
    public DateOnly? TerminationDate { get; init; }
}
