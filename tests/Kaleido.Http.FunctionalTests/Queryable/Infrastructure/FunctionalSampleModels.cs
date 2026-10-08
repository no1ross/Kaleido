using Kaleido.Queryable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Kaleido.Http.FunctionalTests.Queryable.Infrastructure;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FunctionalRecordStatus
{
    Unknown,
    Draft,
    Active,
    Suspended,
    Retired
}

public sealed class FunctionalRecordContext : IQueryContext
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
    DisplayName = "Grid View",
    Description = "Grid view for functional records.",
    Version = "1.0.0",
    DefaultSortField = nameof(FunctionalRecordContext.Id))]
[Pageable(DefaultSize = 3, MaxSize = 10)]
public sealed class FunctionalRecordGridView : IQueryViewSource<FunctionalRecordContextSource, FunctionalRecordContext, FunctionalRecordView, FunctionalRecordViewParameters>
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

public sealed class FunctionalRecordViewParameters : IQueryParameters
{
    [Required]
    [Description("Category to label the grid view request.")]
    public string Category { get; init; } = string.Empty;
}

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Functional Records",
    Description = "Functional records for Queryable HTTP tests.",
    Source = "AspNetCore Functional Test Data")]
[Pageable(DefaultSize = 3, MaxSize = 10)]
public sealed class FunctionalRecordContextSource : IQuerySource<FunctionalRecordContext>
{
    private readonly FunctionalRecordData _data;

    public FunctionalRecordContextSource(FunctionalRecordData data)
    {
        _data = data;
    }

    public IQueryable<FunctionalRecordContext> CreateQuery(QueryExecutionContext executionContext) =>
        _data.Records.AsQueryable();
}

/// <summary>
/// A delegated source over the same query context as <see cref="FunctionalRecordContextSource"/>
/// (a context may back several sources). It stands in for a facade that calls a downstream service:
/// it uses its parameter, maps to its own result, and returns the downstream paging information
/// (a fixed total of 42) — Kaleido must pass that through untouched.
/// </summary>
[QuerySource(
    Version = "1.0.0",
    DisplayName = "Functional Record Summaries",
    Description = "Delegated summaries of functional records in one category.")]
[Pageable(DefaultSize = 2, MaxSize = 5)]
public sealed class FunctionalRecordSummarySource(FunctionalRecordData data)
    : IDelegatedQuerySource<FunctionalRecordContext, FunctionalRecordSummary, FunctionalRecordSummaryParameters>
{
    public const int DownstreamTotalCount = 42;

    public Task<QueryResult<FunctionalRecordSummary>> ExecuteAsync(
        IQueryRequest<FunctionalRecordSummaryParameters> request,
        CancellationToken cancellationToken = default)
    {
        var offset = request.Query?.Page?.Offset ?? 0;
        var size = request.Query?.Page?.Size ?? 2;

        var results =
            data.Records
                .Where(x => x.Category == request.ViewParameters?.Category)
                .Select(x => new FunctionalRecordSummary { Label = $"{x.Code} ({x.Region})" })
                .ToArray();

        return Task.FromResult(new QueryResult<FunctionalRecordSummary>(DownstreamTotalCount, offset, size, results));
    }
}

public sealed class FunctionalRecordSummary
{
    public string Label { get; init; } = string.Empty;
}

public sealed class FunctionalRecordSummaryParameters : IQueryParameters
{
    [Required]
    [Description("Category the downstream search is scoped to.")]
    public string Category { get; init; } = string.Empty;
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
