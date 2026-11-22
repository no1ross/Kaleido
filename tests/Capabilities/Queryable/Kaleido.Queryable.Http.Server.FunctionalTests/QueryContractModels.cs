using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Queryable.Http.Server.FunctionalTests.Contract;

public enum RecordStatus
{
    Unknown,
    Draft,
    Active,
    Suspended,
    Retired,
}

public sealed class ContractRecord : IQueryContext
{
    [Filterable(FilterOperator.Equals, FilterOperator.In, FilterOperator.NotIn)]
    [Sortable]
    public int Id { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.Contains, FilterOperator.StartsWith, FilterOperator.EndsWith, FilterOperator.NotContains)]
    [Searchable(Priority = 1, MatchMode = MatchMode.Contains)]
    [Sortable]
    public string Code { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.Contains)]
    [Searchable(Priority = 2, MatchMode = MatchMode.Contains)]
    [Sortable]
    [Description("The record's display name")]
    public string Name { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.In)]
    [Sortable]
    public string Category { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.IsTrue, FilterOperator.IsFalse)]
    [Sortable]
    public bool IsActive { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.Between)]
    [Sortable]
    public decimal Amount { get; init; }

    [Filterable(FilterOperator.Equals)]
    [Sortable]
    public RecordStatus Status { get; init; }

    [Searchable(Priority = 3, MatchMode = MatchMode.Contains)]
    public string Region { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.GreaterThanOrEqual)]
    [Sortable]
    public DateOnly EffectiveDate { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.IsNull, FilterOperator.IsNotNull)]
    public float? NullableScore { get; init; }

    [Filterable(FilterOperator.Equals)]
    public Guid ExternalId { get; init; }

    [Filterable(FilterOperator.GreaterThanOrEqual)]
    public DateTime CreatedAt { get; init; }
}

public sealed class ContractRecords
{
    public IReadOnlyList<ContractRecord> Records { get; } =
    [
        new() { Id = 1, Code = "AL-001", Name = "Alpha One", Category = "Alpha", IsActive = true, Amount = 10m, Status = RecordStatus.Active, Region = "East", EffectiveDate = new DateOnly(2024, 1, 1), NullableScore = 1.1f, ExternalId = ExternalIdOf(1), CreatedAt = CreatedAtOf(1) },
        new() { Id = 2, Code = "BE-002", Name = "Beta Two", Category = "Beta", IsActive = false, Amount = 25m, Status = RecordStatus.Draft, Region = "West", EffectiveDate = new DateOnly(2024, 1, 5), NullableScore = null, ExternalId = ExternalIdOf(2), CreatedAt = CreatedAtOf(2) },
        new() { Id = 3, Code = "GA-003", Name = "Gamma Three", Category = "Gamma", IsActive = true, Amount = 40m, Status = RecordStatus.Active, Region = "South", EffectiveDate = new DateOnly(2024, 2, 1), NullableScore = 3.3f, ExternalId = ExternalIdOf(3), CreatedAt = CreatedAtOf(3) },
        new() { Id = 4, Code = "AL-004", Name = "Alpha Four", Category = "Alpha", IsActive = true, Amount = 55m, Status = RecordStatus.Suspended, Region = "Central", EffectiveDate = new DateOnly(2024, 2, 15), NullableScore = 4.4f, ExternalId = ExternalIdOf(4), CreatedAt = CreatedAtOf(4) },
        new() { Id = 5, Code = "DE-005", Name = "Delta Five", Category = "Delta", IsActive = false, Amount = 70m, Status = RecordStatus.Retired, Region = "North", EffectiveDate = new DateOnly(2024, 3, 1), NullableScore = null, ExternalId = ExternalIdOf(5), CreatedAt = CreatedAtOf(5) },
        new() { Id = 6, Code = "GA-006", Name = "Gamma Six", Category = "Gamma", IsActive = true, Amount = 85m, Status = RecordStatus.Active, Region = "East", EffectiveDate = new DateOnly(2024, 3, 20), NullableScore = 6.6f, ExternalId = ExternalIdOf(6), CreatedAt = CreatedAtOf(6) },
    ];

    public static Guid ExternalIdOf(int id) => new($"00000000-0000-0000-0000-{id:D12}");
    private static DateTime CreatedAtOf(int id) => new(2024, 1, id, 9, 30, 0, DateTimeKind.Utc);
}

[QuerySource(DisplayName = "Contract records", Description = "Record query contract over HTTP", DefaultSortField = nameof(ContractRecord.Id))]
[Pageable(DefaultSize = 3, MaxSize = 10)]
public sealed class ContractSource(ContractRecords data) : IQuerySource<ContractRecord>
{
    public IQueryable<ContractRecord> CreateQuery(QueryExecutionContext executionContext) => data.Records.Reverse().AsQueryable();
}

[QuerySource(DisplayName = "Unpaged records", Description = "Contract records without paging")]
public sealed class UnpagedContractSource(ContractRecords data) : IQuerySource<ContractRecord>
{
    public IQueryable<ContractRecord> CreateQuery(QueryExecutionContext executionContext) => data.Records.AsQueryable();
}

[QuerySource(DisplayName = "Async contract records", Description = "Asynchronous record query contract", DefaultSortField = nameof(ContractRecord.Id))]
[Pageable(DefaultSize = 3, MaxSize = 10)]
public sealed class AsyncContractSource(ContractRecords data) : IQuerySourceAsync<ContractRecord>
{
    public async Task<IQueryable<ContractRecord>> CreateQueryAsync(QueryExecutionContext executionContext, CancellationToken cancellationToken)
    {
        await Task.Yield();
        return data.Records.Reverse().AsQueryable();
    }
}

public sealed class CodeListItem
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
}

[QueryView(DisplayName = "Code list", Description = "Contract codes ordered by code", DefaultSortField = nameof(ContractRecord.Code))]
[Pageable(DefaultSize = 2, MaxSize = 4)]
public sealed class CodeListView : IQueryViewSourceAsync<AsyncContractSource, ContractRecord, CodeListItem>
{
    public async Task<IQueryable<CodeListItem>> CreateViewAsync(IQueryable<ContractRecord> query, QueryExecutionContext executionContext, CancellationToken cancellationToken)
    {
        await Task.Yield();
        return query.Select(record => new CodeListItem { Id = record.Id, Code = record.Code });
    }
}

public sealed class GridParameters : IQueryParameters
{
    [Required]
    [Description("Category for the grid request")]
    public string Category { get; init; } = string.Empty;
}

[QueryView(DisplayName = "Grid", Description = "A grid requiring a category parameter", DefaultSortField = nameof(ContractRecord.Id))]
[Pageable(DefaultSize = 3, MaxSize = 10)]
public sealed class GridView : IQueryViewSource<ContractSource, ContractRecord, CodeListItem, GridParameters>
{
    public IQueryable<CodeListItem> CreateView(IQueryable<ContractRecord> query, QueryExecutionContext executionContext) =>
        query.Select(record => new CodeListItem { Id = record.Id, Code = record.Code });
}

public sealed class SummaryParameters : IQueryParameters
{
    [Required]
    [Description("Category for the delegated query")]
    public string Category { get; init; } = string.Empty;
}

public sealed record ContractSummary
{
    public string Label { get; init; } = string.Empty;
}

[QuerySource(DisplayName = "Contract summaries", Description = "Delegated summaries with downstream totals")]
[Pageable(DefaultSize = 2, MaxSize = 5)]
public sealed class ContractSummarySource(ContractRecords data) : IDelegatedQuerySource<ContractRecord, ContractSummary, SummaryParameters>
{
    public Task<QueryResult<ContractSummary>> ExecuteAsync(IQueryRequest<SummaryParameters> request, CancellationToken cancellationToken)
    {
        var offset = request.Query?.Page?.Offset ?? 0;
        var size = request.Query?.Page?.Size ?? 2;
        var results = data.Records.Where(record => record.Category == request.ViewParameters?.Category)
            .Select(record => new ContractSummary { Label = $"{record.Code} ({record.Region})" }).ToArray();
        return Task.FromResult(new QueryResult<ContractSummary>(42, offset, size, results));
    }
}
