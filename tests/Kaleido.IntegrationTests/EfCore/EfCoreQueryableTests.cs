using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.IntegrationTests.EfCore;

// Kaleido's queryable runtime against a real EF Core provider (SQLite, translated to SQL). Unit tests
// run the generated expressions with LINQ to Objects, which accepts anything; a provider rejects
// expressions it cannot translate, and SQL's semantics (nulls, collation, paging) are its own. These
// tests prove a consumer's EF Core source works end to end through IQueryableService.
public sealed class EfCoreQueryableTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private ServiceProvider? _provider;

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContext<MemberDbContext>(o => o.UseSqlite(_connection));
        services.AddKaleido(new ConfigurationBuilder().Build(), o =>
        {
            o.ServiceName = "members";
            o.AuthorizationMode = KaleidoAuthorizationMode.None;
            o.Assemblies = [typeof(EfCoreQueryableTests).Assembly];
            o.TypeFilter = type => type.Namespace == typeof(EfCoreQueryableTests).Namespace;
        });

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MemberDbContext>();
        await db.Database.EnsureCreatedAsync();
        db.Members.AddRange(
            Member(1, "Ava Stone", "Austin", MemberPlan.Gold, true, 2024, 1, 0.9),
            Member(2, "Ben Ortiz", "Boston", MemberPlan.Silver, false, 2024, 2, null),
            Member(3, "Cara Stone", "Austin", MemberPlan.Bronze, true, 2024, 3, 0.4),
            Member(4, "Dan Reyes", "Denver", MemberPlan.Gold, true, 2024, 4, null),
            Member(5, "Eve Marsh", "Boston", MemberPlan.Gold, false, 2024, 5, 0.7));
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        await _connection.DisposeAsync();
    }

    private static MemberEntity Member(int id, string name, string city, MemberPlan plan, bool active, int year, int month, double? score) =>
        new()
        {
            Id = id,
            Name = name,
            City = city,
            Plan = plan,
            IsActive = active,
            JoinedUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc),
            Score = score,
            ExternalId = new Guid($"00000000-0000-0000-0000-{id:D12}"),
        };

    private async Task<QueryResult<TResult>> QueryAsync<TQuery, TResult>(QueryBody body)
        where TQuery : class
        where TResult : class
    {
        using var scope = _provider!.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IQueryableService>()
            .QueryAsync<TQuery, TResult>(new QueryRequest(body), TestContext.Current.CancellationToken);
    }

    private async Task<int[]> IdsAsync(QueryBody body) =>
        (await QueryAsync<MemberSource, MemberQueryContext>(body with { Page = body.Page ?? new QueryPage(50, 0) })).Results.Select(x => x.Id).ToArray();

    public static TheoryData<string, FilterOperator, object?[], int[]> Conditions() =>
        new()
        {
            { nameof(MemberQueryContext.Name), FilterOperator.Contains, ["Stone"], [1, 3] },
            { nameof(MemberQueryContext.Name), FilterOperator.StartsWith, ["Da"], [4] },
            { nameof(MemberQueryContext.Name), FilterOperator.EndsWith, ["sh"], [5] },
            { nameof(MemberQueryContext.City), FilterOperator.NotEquals, ["Austin"], [2, 4, 5] },
            { nameof(MemberQueryContext.City), FilterOperator.In, ["Austin", "Denver"], [1, 3, 4] },
            { nameof(MemberQueryContext.Plan), FilterOperator.Equals, [MemberPlan.Gold], [1, 4, 5] },
            { nameof(MemberQueryContext.Plan), FilterOperator.In, [MemberPlan.Silver, MemberPlan.Bronze], [2, 3] },
            { nameof(MemberQueryContext.Id), FilterOperator.Between, [2, 4], [2, 3, 4] },
            { nameof(MemberQueryContext.IsActive), FilterOperator.IsFalse, [], [2, 5] },
            { nameof(MemberQueryContext.JoinedUtc), FilterOperator.GreaterThanOrEqual, [new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc)], [3, 4, 5] },
            { nameof(MemberQueryContext.Score), FilterOperator.IsNull, [], [2, 4] },
            { nameof(MemberQueryContext.Score), FilterOperator.GreaterThan, [0.5], [1, 5] },
            { nameof(MemberQueryContext.ExternalId), FilterOperator.Equals, [new Guid("00000000-0000-0000-0000-000000000003")], [3] },
        };

    [Theory]
    [MemberData(nameof(Conditions))]
    public async Task AFilter_TranslatesToSql_AndSelectsTheMatchingRows(string field, FilterOperator op, object?[] values, int[] expected)
    {
        var ids = await IdsAsync(new QueryBody(Filter: QueryFilterNode.CreateCondition(field, op, values)));

        Assert.Equal(expected, ids);
    }

    [Fact]
    public async Task GroupsSearchAndSorts_TranslateTogether()
    {
        // (Gold OR City = Boston) AND active, searched for "a", sorted by city then name descending.
        var body =
            new QueryBody(
                SearchText: "a",
                Filter: new QueryFilterNode(
                    null,
                    new QueryFilterGroup(
                        LogicalOperator.And,
                        [
                            new QueryFilterNode(
                                null,
                                new QueryFilterGroup(
                                    LogicalOperator.Or,
                                    [
                                        QueryFilterNode.CreateCondition(nameof(MemberQueryContext.Plan), FilterOperator.Equals, MemberPlan.Gold),
                                        QueryFilterNode.CreateCondition(nameof(MemberQueryContext.City), FilterOperator.Equals, "Boston"),
                                    ])),
                            QueryFilterNode.CreateCondition(nameof(MemberQueryContext.IsActive), FilterOperator.IsTrue),
                        ])),
                Sort:
                [
                    new QuerySort(nameof(MemberQueryContext.City), SortDirection.Ascending),
                    new QuerySort(nameof(MemberQueryContext.Name), SortDirection.Descending),
                ]);

        var ids = await IdsAsync(body);

        Assert.Equal([1, 4], ids);
    }

    [Fact]
    public async Task APage_IsTakenInTheDefaultOrder_WithTheTotalOfAllMatchingRows()
    {
        var first = await QueryAsync<MemberSource, MemberQueryContext>(new QueryBody());
        var last = await QueryAsync<MemberSource, MemberQueryContext>(new QueryBody(Page: new QueryPage(2, 4)));

        Assert.Equal([1, 2], first.Results.Select(x => x.Id));
        Assert.Equal(5, first.TotalCount);
        Assert.Equal([5], last.Results.Select(x => x.Id));
        Assert.Equal(5, last.TotalCount);
    }

    [Fact]
    public async Task AView_ProjectsAfterTheFilter_AndPagesInItsOwnOrder()
    {
        var result =
            await QueryAsync<MemberCardView, MemberCard>(
                new QueryBody(Filter: QueryFilterNode.CreateCondition(nameof(MemberQueryContext.Plan), FilterOperator.Equals, MemberPlan.Gold)));

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(["Ava Stone (Austin)", "Dan Reyes (Denver)", "Eve Marsh (Boston)"], result.Results.Select(x => x.Label));
    }
}
