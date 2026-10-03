using Kaleido.Http.Queryable;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Infrastructure;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Client;

public sealed class QueryableClientTests : IClassFixture<QueryableAspNetCoreFixture>
{
    private readonly IKaleidoQueryableClientFactory _factory;

    public QueryableClientTests(QueryableAspNetCoreFixture fixture)
    {
        _factory = fixture.ClientFactory;
    }

    // ---------------------------------------------------------------------------
    // GetRegistryAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetRegistryAsync_ReturnsAllContexts()
    {
        var registry = await _factory.GetClient("test").GetRegistryAsync();

        Assert.Contains(registry, r => r.Name == "functional-records");
    }

    [Fact]
    public async Task GetRegistryAsync_ContainsExpectedQueryUrls()
    {
        var registry = await _factory.GetClient("test").GetRegistryAsync();

        var record = Assert.Single(registry, r => r.Name == "functional-records");
        Assert.Equal("/kaleido/queryable/functional-records/query", record.QueryUrl);
    }

    // ---------------------------------------------------------------------------
    // QueryViewAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task QueryViewAsync_ReturnsResults()
    {
        var result = await _factory.GetClient("test").QueryViewAsync<FunctionalRecordViewParameters, FunctionalRecordView>(
            "functional-records",
            "grid",
            new QueryApiRequest<FunctionalRecordViewParameters>(
                new FunctionalRecordViewParameters { Category = "Alpha" },
                new QueryApiBody()));

        Assert.True(result.TotalCount > 0);
        Assert.NotEmpty(result.Results);
    }

    // ---------------------------------------------------------------------------
    // QueryContextAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task QueryContextAsync_ReturnsResults()
    {
        var result = await _factory.GetClient("test").QueryContextAsync<FunctionalRecordContext>(
            "functional-records",
            new QueryApiRequest(new QueryApiBody()));

        Assert.True(result.TotalCount > 0);
        Assert.NotEmpty(result.Results);
    }
}
