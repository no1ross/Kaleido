using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.History.Queryable.Contexts;

[QueryContext(
    Name = "prior-auth-records",
    DisplayName = "Prior Auth Records",
    Version = "1.0.0",
    Source = "Prior Auth History Service")]
public sealed class PriorAuthRecordQueryContext
{
    [Key]
    public Guid PriorAuthRecordId { get; init; }

    public Guid ProcessId { get; init; }

    [Filterable(FilterOperator.Equals)]
    public string ProcessorName { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    [Sortable]
    public PriorAuthorizationStatus Status { get; init; }

    [Searchable(Priority = 1, MatchMode = MatchMode.Contains)]
    [Sortable]
    public string MemberDisplayName { get; init; } = string.Empty;

    [Searchable(Priority = 2, MatchMode = MatchMode.Exact)]
    public string MemberNumber { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThanOrEqual)]
    [Sortable]
    public DateOnly DateOfService { get; init; }

    [Sortable]
    public string PrimaryProcedureCode { get; init; } = string.Empty;

    [Sortable]
    public string PrimaryProcedureDescription { get; init; } = string.Empty;

    [Sortable]
    public DateTimeOffset CreatedUtc { get; init; }

    [Sortable]
    public DateTimeOffset LastUpdatedUtc { get; init; }
}
