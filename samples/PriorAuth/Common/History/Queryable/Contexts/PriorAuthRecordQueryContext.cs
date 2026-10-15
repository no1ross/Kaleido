using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;

namespace Kaleido.Samples.PriorAuth.History.Queryable.Contexts;

public sealed class PriorAuthRecordQueryContext : IQueryContext
{
    [Key]
    [Filterable(FilterOperator.Equals)]
    [Description("The prior authorization process this record belongs to; there is one record per process.")]
    public Guid ProcessId { get; init; }

    [Filterable(FilterOperator.Equals)]
    [Description("The processor that recorded the latest snapshot, such as intake or radiology.")]
    public string ProcessorName { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.IsNull)]
    [Description("The prior authorization assigned by the product processor; empty while the request is in Intake.")]
    public Guid? PriorAuthorizationId { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    [Sortable]
    [Description("The current status of the prior authorization request.")]
    public PriorAuthorizationStatus Status { get; init; }

    [Searchable(Priority = 1, MatchMode = MatchMode.Contains)]
    [Sortable]
    [Description("The display name of the member the request is for.")]
    public string MemberDisplayName { get; init; } = string.Empty;

    [Searchable(Priority = 2, MatchMode = MatchMode.Exact)]
    [Description("The member number of the member the request is for.")]
    public string MemberNumber { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThanOrEqual)]
    [Sortable]
    [Description("The date the service is planned, when known.")]
    public DateOnly? DateOfService { get; init; }

    [Sortable]
    [Description("The procedure code of the main service requested.")]
    public string PrimaryProcedureCode { get; init; } = string.Empty;

    [Sortable]
    [Description("A readable description of the main service requested.")]
    public string PrimaryProcedureDescription { get; init; } = string.Empty;

    [Sortable]
    [Description("When the record was first created, in UTC.")]
    public DateTimeOffset CreatedUtc { get; init; }

    [Sortable]
    [Description("When the latest snapshot was recorded, in UTC.")]
    public DateTimeOffset LastUpdatedUtc { get; init; }
}
