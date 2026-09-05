using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Infrastructure;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FunctionalRecordStatus
{
    Unknown,
    Draft,
    Active,
    Suspended,
    Retired
}

[QueryContext(
    Name = "functional-records",
    DisplayName = "Functional Records",
    Description = "Functional records for Queryable HTTP tests.",
    Version = "1.0.0",
    Source = "AspNetCore Functional Test Data",
    Kind = QueryContextKind.Direct)]
[Pageable(DefaultSize = 3, MaxSize = 10)]
public sealed class FunctionalRecordContext
{
    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    [Sortable]
    public int Id { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.Contains, FilterOperator.StartsWith)]
    [Searchable(Priority = 1, MatchMode = MatchMode.Contains)]
    [Sortable]
    public string Code { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.Contains)]
    [Searchable(Priority = 2, MatchMode = MatchMode.Contains)]
    [Sortable]
    public string Name { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    [Sortable]
    public string Category { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.IsTrue, FilterOperator.IsFalse)]
    [Sortable]
    public bool IsActive { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.GreaterThanOrEqual, FilterOperator.Between)]
    [Sortable]
    public decimal Amount { get; init; }

    [Filterable(FilterOperator.Equals)]
    [Sortable]
    public FunctionalRecordStatus Status { get; init; }

    [Searchable(Priority = 3, MatchMode = MatchMode.Contains)]
    public string Region { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.GreaterThanOrEqual)]
    [Sortable]
    public DateOnly EffectiveDate { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.IsNull, FilterOperator.IsNotNull)]
    public float? NullableScore { get; init; }
}

[QueryView(
    Name = "grid",
    DisplayName = "Grid View",
    Description = "Grid view for functional records.",
    Version = "1.0.0",
    DefaultSortField = nameof(FunctionalRecordContext.Id))]
[Pageable(DefaultSize = 3, MaxSize = 10)]
public sealed class FunctionalRecordGridView : IQueryViewSource<FunctionalRecordContext, FunctionalRecordView, FunctionalRecordViewParameters>
{
    public IQueryable<FunctionalRecordView> CreateView(
        IQueryable<FunctionalRecordContext> query,
        QueryExecutionContext executionContext)
    {
        return query.Select(x => new FunctionalRecordView
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            Category = x.Category,
            IsActive = x.IsActive,
            Amount = x.Amount,
            Status = x.Status,
            Region = x.Region,
            EffectiveDate = x.EffectiveDate,
            NullableScore = x.NullableScore
        });
    }
}

public sealed class FunctionalRecordView
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public decimal Amount { get; init; }
    public FunctionalRecordStatus Status { get; init; }
    public string Region { get; init; } = string.Empty;
    public DateOnly EffectiveDate { get; init; }
    public float? NullableScore { get; init; }
}

public sealed class FunctionalRecordViewParameters
{
    [Required]
    [Description("Category to label the grid view request.")]
    public string Category { get; init; } = string.Empty;
}

public sealed class FunctionalRecordContextSource : IQueryContextSource<FunctionalRecordContext>
{
    private readonly FunctionalRecordData _data;

    public FunctionalRecordContextSource(FunctionalRecordData data)
    {
        _data = data;
    }

    public IQueryable<FunctionalRecordContext> CreateQuery(QueryExecutionContext executionContext) =>
        _data.Records.AsQueryable();
}

public sealed class FunctionalRecordData
{
    public IReadOnlyList<FunctionalRecordContext> Records { get; } =
    [
        new() { Id = 1, Code = "AL-001", Name = "Alpha One", Category = "Alpha", IsActive = true, Amount = 10m, Status = FunctionalRecordStatus.Active, Region = "East", EffectiveDate = new DateOnly(2024, 1, 1), NullableScore = 1.1f },
        new() { Id = 2, Code = "BE-002", Name = "Beta Two", Category = "Beta", IsActive = false, Amount = 25m, Status = FunctionalRecordStatus.Draft, Region = "West", EffectiveDate = new DateOnly(2024, 1, 5), NullableScore = null },
        new() { Id = 3, Code = "GA-003", Name = "Gamma Three", Category = "Gamma", IsActive = true, Amount = 40m, Status = FunctionalRecordStatus.Active, Region = "South", EffectiveDate = new DateOnly(2024, 2, 1), NullableScore = 3.3f },
        new() { Id = 4, Code = "AL-004", Name = "Alpha Four", Category = "Alpha", IsActive = true, Amount = 55m, Status = FunctionalRecordStatus.Suspended, Region = "Central", EffectiveDate = new DateOnly(2024, 2, 15), NullableScore = 4.4f },
        new() { Id = 5, Code = "DE-005", Name = "Delta Five", Category = "Delta", IsActive = false, Amount = 70m, Status = FunctionalRecordStatus.Retired, Region = "North", EffectiveDate = new DateOnly(2024, 3, 1), NullableScore = null },
        new() { Id = 6, Code = "GA-006", Name = "Gamma Six", Category = "Gamma", IsActive = true, Amount = 85m, Status = FunctionalRecordStatus.Active, Region = "East", EffectiveDate = new DateOnly(2024, 3, 20), NullableScore = 6.6f }
    ];
}
