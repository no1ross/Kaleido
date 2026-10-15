using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;

namespace Kaleido.Samples.SQLite;

public enum RecordStatus
{
    [Description("Unknown")]
    Unknown,
    [Description("Draft")]
    Draft,
    [Description("Active")]
    Active,
    [Description("Suspended")]
    Suspended,
    [Description("Retired")]
    Retired
}

public sealed class SampleKaleidoRecord : IQueryContext
{
    [Key]
    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between, FilterOperator.In, FilterOperator.NotIn)]
    [Sortable]
    [Description("The record's numeric ID.")]
    public int Id { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    [Searchable(Priority = 1, MatchMode = MatchMode.Exact)]
    [Description("The record's external ID (a GUID).")]
    public Guid ExternalId { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.Contains, FilterOperator.NotContains, FilterOperator.StartsWith, FilterOperator.EndsWith, FilterOperator.In, FilterOperator.NotIn)]
    [Searchable(Priority = 2, MatchMode = MatchMode.Contains)]
    [Sortable]
    [Description("The record's short code.")]
    public string Code { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.Contains, FilterOperator.NotContains, FilterOperator.StartsWith, FilterOperator.EndsWith)]
    [Searchable(Priority = 3, MatchMode = MatchMode.StartsWith)]
    [Sortable]
    [Description("The record's name.")]
    public string Name { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.In, FilterOperator.NotIn)]
    [Searchable(Priority = 4, MatchMode = MatchMode.EndsWith)]
    [Sortable]
    [Description("The category the record belongs to.")]
    public string Category { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.IsTrue, FilterOperator.IsFalse)]
    [Sortable]
    [Description("Whether the record is active.")]
    public bool IsActive { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between, FilterOperator.In, FilterOperator.NotIn)]
    [Sortable]
    [Description("A whole-number quantity.")]
    public int Quantity { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between)]
    [Sortable]
    [Description("A decimal amount.")]
    public decimal Amount { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between)]
    [Sortable]
    [Description("A floating-point rate.")]
    public double Rate { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between)]
    [Sortable]
    [Description("A floating-point score.")]
    public float Score { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between)]
    [Sortable]
    [Description("The date the record takes effect.")]
    public DateOnly EffectiveDate { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between)]
    [Sortable]
    [Description("When the record was created.")]
    public DateTime CreatedAt { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between, FilterOperator.IsNull, FilterOperator.IsNotNull)]
    [Sortable]
    [Description("The date the record expires, if it does.")]
    public DateOnly? ExpirationDate { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.In, FilterOperator.NotIn)]
    [Sortable]
    [Description("The record's lifecycle status.")]
    public RecordStatus Status { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.In, FilterOperator.NotIn)]
    [Sortable]
    [Description("The record's priority.")]
    public int Priority { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.In, FilterOperator.NotIn)]
    [Searchable(Priority = 5, MatchMode = MatchMode.Exact)]
    [Description("The region the record belongs to.")]
    public string Region { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.In, FilterOperator.NotIn)]
    [Searchable(Priority = 6, MatchMode = MatchMode.Exact)]
    [Description("The group the record belongs to.")]
    public string GroupName { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between)]
    [Sortable]
    [Description("The record's version number.")]
    public long Version { get; init; }

    [Searchable(Priority = 7, MatchMode = MatchMode.Contains)]
    [Description("Free-text notes about the record.")]
    public string Notes { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between, FilterOperator.IsNull, FilterOperator.IsNotNull)]
    [Description("An optional floating-point score that may be empty.")]
    public float? NullableScore { get; init; }
}
