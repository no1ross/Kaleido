using Kaleido.Samples.PriorAuth.Compliance;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;

namespace Kaleido.Samples.PriorAuth.Member.Queryable.Contexts;

public sealed class MemberQueryContext : IQueryContext
{
    [Key]
    [Description("The member's enrollment (plan coverage) this row describes.")]
    public Guid MemberEnrollmentId { get; init; }

    [Description("The member who holds this enrollment.")]
    public Guid MemberId { get; init; }

    [Searchable(
        Priority = 1,
        MatchMode = MatchMode.Exact)]
    [Sortable]
    [Description("The member ID printed on the member's card.")]
    public string MemberNumber { get; init; } = string.Empty;

    [Searchable(
        Priority = 2,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    [Phi]
    [Description("The member's first name.")]
    public string FirstName { get; init; } = string.Empty;

    [Searchable(
        Priority = 3,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    [Phi]
    [Description("The member's last name.")]
    public string LastName { get; init; } = string.Empty;

    [Searchable(
        Priority = 4,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    [Phi]
    [Description("The member's full name for display.")]
    public string DisplayName { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals)]
    [Sortable]
    [Phi]
    [Description("The member's date of birth.")]
    public DateOnly DateOfBirth { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Sortable]
    [Description("The state the member's coverage was issued in.")]
    public string IssuanceState { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Description("The line of business of the enrollment's plan (commercial, Medicare, Medicaid or exchange).")]
    public LineOfBusiness LineOfBusiness { get; init; }

    [Sortable]
    [Description("The plan the member is enrolled in.")]
    public string PlanId { get; init; } = string.Empty;

    [Sortable]
    [Description("The name of the plan the member is enrolled in.")]
    public string PlanName { get; init; } = string.Empty;

    [Sortable]
    [Description("The date the enrollment's coverage starts.")]
    public DateOnly EffectiveDate { get; init; }

    [Sortable]
    [Description("The date the enrollment's coverage ends, if it has an end date.")]
    public DateOnly? TerminationDate { get; init; }
}
