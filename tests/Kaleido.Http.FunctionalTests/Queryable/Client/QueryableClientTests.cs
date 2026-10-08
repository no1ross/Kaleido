using Kaleido.Http.Queryable;
using Kaleido.Http.FunctionalTests.Queryable.Fixtures;
using Kaleido.Http.FunctionalTests.Queryable.Infrastructure;

namespace Kaleido.Http.FunctionalTests.Queryable.Client;

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
    public async Task GetRegistryAsync_ReturnsAllSources()
    {
        var registry = await _factory.GetClient("test").GetRegistryAsync();

        Assert.Contains(registry, r => r.Name == "FunctionalRecordContextSource");
    }

    [Fact]
    public async Task GetRegistryAsync_ContainsExpectedQueryUrls()
    {
        var registry = await _factory.GetClient("test").GetRegistryAsync();

        var record = Assert.Single(registry, r => r.Name == "FunctionalRecordContextSource");
        Assert.Equal("/kaleido/queryable/functionalrecordcontextsource/query", record.QueryUrl);
    }

    // ---------------------------------------------------------------------------
    // QueryViewAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task QueryViewAsync_ReturnsResults()
    {
        var result = await _factory.GetClient("test").QueryViewAsync<FunctionalRecordViewParameters, FunctionalRecordView>(
            "FunctionalRecordContextSource",
            "FunctionalRecordGridView",
            new QueryApiRequest<FunctionalRecordViewParameters>(
                new FunctionalRecordViewParameters { Category = "Alpha" },
                new QueryApiBody()));

        Assert.True(result.TotalCount > 0);
        Assert.NotEmpty(result.Results);
    }

    // ---------------------------------------------------------------------------
    // QuerySourceAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task QuerySourceAsync_ReturnsResults()
    {
        var result = await _factory.GetClient("test").QuerySourceAsync<FunctionalRecordContext>(
            "FunctionalRecordContextSource",
            new QueryApiRequest(new QueryApiBody()));

        Assert.True(result.TotalCount > 0);
        Assert.NotEmpty(result.Results);
    }
}
