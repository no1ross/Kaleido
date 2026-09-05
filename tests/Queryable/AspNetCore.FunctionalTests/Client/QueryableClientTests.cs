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
    public async Task GetRegistryAsync_ContainsExpectedMetadataUrls()
    {
        var registry = await _factory.GetClient("test").GetRegistryAsync();

        var record = Assert.Single(registry, r => r.Name == "functional-records");
        Assert.Equal("/kaleido/queryable/functional-records/metadata", record.MetadataUrl);
        Assert.Equal("/kaleido/queryable/functional-records/query", record.QueryUrl);
    }

    // ---------------------------------------------------------------------------
    // GetContextMetadataAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetContextMetadataAsync_ReturnsFullMetadata()
    {
        var metadata = await _factory.GetClient("test").GetContextMetadataAsync("functional-records");

        Assert.Equal("functional-records", metadata.Name);
        Assert.Equal("Functional records for Queryable HTTP tests.", metadata.Description);
        Assert.Equal("Functional Records", metadata.DisplayName);
        Assert.Equal("1.0.0", metadata.Version);
        Assert.NotEmpty(metadata.Fields);
        Assert.Contains(metadata.Views, v => v.Name == "grid");
    }

    [Fact]
    public async Task GetContextMetadataAsync_WhenContextNotFound_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _factory.GetClient("test").GetContextMetadataAsync("does-not-exist"));
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
                new QueryBody()));

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
            new QueryApiRequest(new QueryBody()));

        Assert.True(result.TotalCount > 0);
        Assert.NotEmpty(result.Results);
    }
}
