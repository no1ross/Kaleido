using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Attributes;

namespace Kaleido.Shared;

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

[KaleidoRecord("functional-records", "1.0.0", "CSV Functional Test Data")]
[AllowedQuery("active-records", "Returns active records.")]
[AllowedQuery("records-by-category", "Returns records for the supplied category.", "category")]
[AllowedQuery("high-amount-records", "Returns records above the supplied amount.", "minimumAmount")]
[AllowedQuery("effective-on", "Returns records effective on the supplied date.", "effectiveDate")]
[Pageable(25, 500)]
public sealed class SampleKaleidoRecord
{
    [Key]
    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.Gt, FilterOperator.Gte, FilterOperator.Lt, FilterOperator.Lte, FilterOperator.Between, FilterOperator.In, FilterOperator.NotIn)]
    [Sortable]
    public int Id { get; init; }

    [Filterable(FilterOperator.Eq, FilterOperator.In)]
    [Searchable(1, MatchMode.Exact)]
    public Guid ExternalId { get; init; }

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.Contains, FilterOperator.NotContains, FilterOperator.StartsWith, FilterOperator.EndsWith, FilterOperator.In, FilterOperator.NotIn)]
    [Searchable(2, MatchMode.Exact, MatchMode.StartsWith, MatchMode.EndsWith, MatchMode.Contains)]
    [Sortable]
    public string Code { get; init; } = string.Empty;

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.Contains, FilterOperator.NotContains, FilterOperator.StartsWith, FilterOperator.EndsWith)]
    [Searchable(3, MatchMode.Exact, MatchMode.StartsWith, MatchMode.EndsWith, MatchMode.Contains)]
    [Sortable]
    public string Name { get; init; } = string.Empty;

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.In, FilterOperator.NotIn)]
    [Searchable(4, MatchMode.Exact, MatchMode.Contains)]
    [Sortable]
    public string Category { get; init; } = string.Empty;

    [Filterable(FilterOperator.Eq, FilterOperator.IsTrue, FilterOperator.IsFalse)]
    [Sortable]
    public bool IsActive { get; init; }

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.Gt, FilterOperator.Gte, FilterOperator.Lt, FilterOperator.Lte, FilterOperator.Between, FilterOperator.In, FilterOperator.NotIn)]
    [Sortable]
    public int Quantity { get; init; }

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.Gt, FilterOperator.Gte, FilterOperator.Lt, FilterOperator.Lte, FilterOperator.Between)]
    [Sortable]
    public decimal Amount { get; init; }

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.Gt, FilterOperator.Gte, FilterOperator.Lt, FilterOperator.Lte, FilterOperator.Between)]
    [Sortable]
    public double Rate { get; init; }

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.Gt, FilterOperator.Gte, FilterOperator.Lt, FilterOperator.Lte, FilterOperator.Between)]
    [Sortable]
    public float Score { get; init; }

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.Gt, FilterOperator.Gte, FilterOperator.Lt, FilterOperator.Lte, FilterOperator.Between)]
    [Sortable]
    public DateOnly EffectiveDate { get; init; }

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.Gt, FilterOperator.Gte, FilterOperator.Lt, FilterOperator.Lte, FilterOperator.Between)]
    [Sortable]
    public DateTime CreatedAt { get; init; }

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.Gt, FilterOperator.Gte, FilterOperator.Lt, FilterOperator.Lte, FilterOperator.Between, FilterOperator.IsNull, FilterOperator.IsNotNull)]
    [Sortable]
    public DateOnly? ExpirationDate { get; init; }

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.In, FilterOperator.NotIn)]
    [Sortable]
    public RecordStatus Status { get; init; }

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.Gt, FilterOperator.Gte, FilterOperator.Lt, FilterOperator.Lte, FilterOperator.In, FilterOperator.NotIn)]
    [Sortable]
    public int Priority { get; init; }

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.In, FilterOperator.NotIn)]
    [Searchable(5, MatchMode.Exact, MatchMode.Contains)]
    public string Region { get; init; } = string.Empty;

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.In, FilterOperator.NotIn)]
    [Searchable(6, MatchMode.Exact, MatchMode.Contains)]
    public string GroupName { get; init; } = string.Empty;

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.Gt, FilterOperator.Gte, FilterOperator.Lt, FilterOperator.Lte, FilterOperator.Between)]
    [Sortable]
    public long Version { get; init; }

    [Searchable(7, MatchMode.Contains)]
    public string Notes { get; init; } = string.Empty;

    [Filterable(FilterOperator.Eq, FilterOperator.Ne, FilterOperator.Gt, FilterOperator.Gte, FilterOperator.Lt, FilterOperator.Lte, FilterOperator.Between, FilterOperator.IsNull, FilterOperator.IsNotNull)]
    public float? NullableScore { get; init; }
}
