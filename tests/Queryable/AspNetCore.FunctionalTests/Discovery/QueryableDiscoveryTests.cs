using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;
using System.Net;
using System.Net.Http.Json;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Discovery;

public sealed class QueryableDiscoveryTests : IClassFixture<QueryableAspNetCoreFixture>
{
    private readonly HttpClient _client;

    public QueryableDiscoveryTests(QueryableAspNetCoreFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetQueryable_ReturnsOk()
    {
        var response = await _client.GetAsync("/kaleido/queryable");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetQueryable_ReturnsFunctionalRecordSummary()
    {
        var records = await _client.GetFromJsonAsync<QueryableRecordSummary[]>("/kaleido/queryable");

        var record = Assert.Single(records!, x => x.Name == "functional-records");

        Assert.Equal("Functional records for Queryable HTTP tests.", record.Description);
        Assert.Equal("/kaleido/queryable/functional-records/metadata", record.MetadataUrl);
    }

    [Fact]
    public async Task GetRegistry_ReturnsContextAndViewMetadata()
    {
        var response = await _client.GetAsync("/kaleido/queryable/registry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var registry = await response.Content.ReadFromJsonAsync<QueryableRecordResponse[]>();
        var record = Assert.Single(registry!, x => x.Name == "functional-records");
        var view = Assert.Single(record.Views, x => x.Name == "grid");

        Assert.Equal("Grid View", view.DisplayName);
        Assert.Equal("/kaleido/queryable/functional-records/grid/query", view.QueryUrl);
        Assert.Single(view.Parameters!);
    }
}
