using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Member.Queryable.Contexts;

[QueryContext(
    Name = "members",
    DisplayName = "Members",
    Version = "1.0.0",
    Source = "Prior Authorization Member Service")]
public sealed class MemberQueryContext
{
    [Key]
    public Guid MemberEnrollmentId { get; init; }

    public Guid MemberId { get; init; }

    [Searchable(
        Priority = 1,
        MatchMode = MatchMode.Exact)]
    [Sortable]
    public string MemberNumber { get; init; } = string.Empty;

    [Searchable(
        Priority = 2,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    public string FirstName { get; init; } = string.Empty;

    [Searchable(
        Priority = 3,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    public string LastName { get; init; } = string.Empty;

    [Searchable(
        Priority = 4,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    public string DisplayName { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals)]
    [Sortable]
    public DateOnly DateOfBirth { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Sortable]
    public string IssuanceState { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    public LineOfBusiness LineOfBusiness { get; init; }

    [Sortable]
    public string PlanId { get; init; } = string.Empty;

    [Sortable]
    public string PlanName { get; init; } = string.Empty;

    [Sortable]
    public DateOnly EffectiveDate { get; init; }

    [Sortable]
    public DateOnly? TerminationDate { get; init; }
}
