using System.ComponentModel;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.IntegrationTests.EfCore;

public enum MemberPlan
{
    Bronze,
    Silver,
    Gold,
}

public sealed class MemberEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public MemberPlan Plan { get; set; }
    public bool IsActive { get; set; }
    public DateTime JoinedUtc { get; set; }
    public double? Score { get; set; }
    public Guid ExternalId { get; set; }
}

public sealed class MemberDbContext(DbContextOptions<MemberDbContext> options) : DbContext(options)
{
    public DbSet<MemberEntity> Members => Set<MemberEntity>();
}

// The query context is a projection of the entity, as consumers usually write it: Kaleido's filters,
// search, sort and paging must translate through the Select into SQL.
public sealed record MemberQueryContext : IQueryContext
{
    [Filterable(FilterOperator.Equals, FilterOperator.In, FilterOperator.Between)]
    [Sortable]
    public int Id { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.Contains, FilterOperator.StartsWith, FilterOperator.EndsWith)]
    [Searchable(Priority = 1, MatchMode = MatchMode.Contains)]
    [Sortable]
    [Description("The member's name.")]
    public string Name { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.In)]
    [Searchable(Priority = 2, MatchMode = MatchMode.StartsWith)]
    [Sortable]
    public string City { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    [Sortable]
    public MemberPlan Plan { get; init; }

    [Filterable(FilterOperator.IsTrue, FilterOperator.IsFalse)]
    public bool IsActive { get; init; }

    [Filterable(FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan)]
    [Sortable]
    public DateTime JoinedUtc { get; init; }

    [Filterable(FilterOperator.IsNull, FilterOperator.IsNotNull, FilterOperator.GreaterThan)]
    public double? Score { get; init; }

    [Filterable(FilterOperator.Equals)]
    public Guid ExternalId { get; init; }
}

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Members",
    Description = "Members from an EF Core database.",
    DefaultSortField = nameof(MemberQueryContext.Id))]
[Pageable(DefaultSize = 2, MaxSize = 50)]
public sealed class MemberSource(MemberDbContext db) : IQuerySource<MemberQueryContext>
{
    public IQueryable<MemberQueryContext> CreateQuery(QueryExecutionContext executionContext) =>
        db.Members.Select(x => new MemberQueryContext
        {
            Id = x.Id,
            Name = x.Name,
            City = x.City,
            Plan = x.Plan,
            IsActive = x.IsActive,
            JoinedUtc = x.JoinedUtc,
            Score = x.Score,
            ExternalId = x.ExternalId,
        });
}

[QueryView(
    DisplayName = "Member Cards",
    Description = "Member names with their city.",
    Version = "1.0.0",
    DefaultSortField = nameof(MemberQueryContext.Name))]
[Pageable(DefaultSize = 3, MaxSize = 10)]
public sealed class MemberCardView : IQueryViewSource<MemberSource, MemberQueryContext, MemberCard>
{
    public IQueryable<MemberCard> CreateView(IQueryable<MemberQueryContext> query, QueryExecutionContext executionContext) =>
        query.Select(x => new MemberCard { Id = x.Id, Label = x.Name + " (" + x.City + ")" });
}

public sealed record MemberCard
{
    public int Id { get; init; }
    public string Label { get; init; } = string.Empty;
}

// A source that is not EF Core, in the same host: an executor registered for every source must
// serve it too (EF's async operators throw on a query that is not EF's).
public sealed record RegionQueryContext : IQueryContext
{
    [Sortable]
    public string Code { get; init; } = string.Empty;
}

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Regions",
    Description = "Regions from memory.",
    DefaultSortField = nameof(RegionQueryContext.Code))]
[Pageable(DefaultSize = 2, MaxSize = 10)]
public sealed class RegionSource : IQuerySource<RegionQueryContext>
{
    private static readonly string[] Codes = ["north", "south", "east"];

    public IQueryable<RegionQueryContext> CreateQuery(QueryExecutionContext executionContext) =>
        Codes.Select(code => new RegionQueryContext { Code = code }).AsQueryable();
}