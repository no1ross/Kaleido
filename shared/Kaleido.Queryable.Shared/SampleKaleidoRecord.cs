using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable.Attributes;

namespace Kaleido.Queryable.Shared;

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

[QueryContext(Name ="functional-records", DisplayName ="Functional Records", Version = "1.0.0", Source = "CSV Functional Test Data")]
[Pageable(DefaultSize = 25, MaxSize = 500)]
[QueryView(Name = "sample-view", DisplayName = "Sample View", Version = "1.0.0", Description = "Sample view for functional testing.")]
public sealed class SampleKaleidoRecord
{
    [Key]
    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between, FilterOperator.In, FilterOperator.NotIn)]
    [Sortable]
    public int Id { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    [Searchable(Priority = 1, MatchMode = MatchMode.Exact)]
    public Guid ExternalId { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.Contains, FilterOperator.NotContains, FilterOperator.StartsWith, FilterOperator.EndsWith, FilterOperator.In, FilterOperator.NotIn)]
    [Searchable(Priority = 2, MatchMode = MatchMode.Contains)]
    [Sortable]
    public string Code { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.Contains, FilterOperator.NotContains, FilterOperator.StartsWith, FilterOperator.EndsWith)]
    [Searchable(Priority = 3, MatchMode = MatchMode.StartsWith)]
    [Sortable]
    public string Name { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.In, FilterOperator.NotIn)]
    [Searchable(Priority = 4, MatchMode = MatchMode.EndsWith)]
    [Sortable]
    public string Category { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.IsTrue, FilterOperator.IsFalse)]
    [Sortable]
    public bool IsActive { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between, FilterOperator.In, FilterOperator.NotIn)]
    [Sortable]
    public int Quantity { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between)]
    [Sortable]
    public decimal Amount { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between)]
    [Sortable]
    public double Rate { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between)]
    [Sortable]
    public float Score { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between)]
    [Sortable]
    public DateOnly EffectiveDate { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between)]
    [Sortable]
    public DateTime CreatedAt { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between, FilterOperator.IsNull, FilterOperator.IsNotNull)]
    [Sortable]
    public DateOnly? ExpirationDate { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.In, FilterOperator.NotIn)]
    [Sortable]
    public RecordStatus Status { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.In, FilterOperator.NotIn)]
    [Sortable]
    public int Priority { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.In, FilterOperator.NotIn)]
    [Searchable(Priority = 5, MatchMode = MatchMode.Exact)]
    public string Region { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.In, FilterOperator.NotIn)]
    [Searchable(Priority = 6, MatchMode = MatchMode.Exact)]
    public string GroupName { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between)]
    [Sortable]
    public long Version { get; init; }

    [Searchable(Priority = 7, MatchMode = MatchMode.Contains)]
    public string Notes { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between, FilterOperator.IsNull, FilterOperator.IsNotNull)]
    public float? NullableScore { get; init; }
}
