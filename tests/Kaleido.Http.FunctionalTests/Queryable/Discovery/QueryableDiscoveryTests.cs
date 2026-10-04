using System.Net;
using Kaleido.Http.Queryable;
using Kaleido.Http.Registry;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Discovery;

public sealed class QueryableDiscoveryTests : IClassFixture<QueryableAspNetCoreFixture>
{
    private readonly HttpClient _client;

    public QueryableDiscoveryTests(QueryableAspNetCoreFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetRegistry_ReturnsOk()
    {
        var response = await _client.GetAsync("/kaleido/registry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRegistry_ReturnsFunctionalRecord()
    {
        var registry = await _client.GetFromJsonAsync<AggregatedRegistryResponse>("/kaleido/registry", KaleidoJsonOptions.Options);

        var record = Assert.Single(registry!.Queryables, x => x.Name == "functional-records");

        Assert.Equal("kaleido", record.ServiceName);
        Assert.Equal("Functional records for Queryable HTTP tests.", record.Description);
        Assert.Equal("/kaleido/registry", record.RegistryUrl);
    }

    [Fact]
    public async Task GetRegistry_ReturnsContextAndViewMetadata()
    {
        var response = await _client.GetAsync("/kaleido/registry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var registry = await response.Content.ReadFromJsonAsync<AggregatedRegistryResponse>(KaleidoJsonOptions.Options);
        var record = Assert.Single(registry!.Queryables, x => x.Name == "functional-records");
        var view = Assert.Single(record.Views, x => x.Name == "grid");

        Assert.Equal("/kaleido/queryable/functional-records/query", record.QueryUrl);
        Assert.Equal("Grid View", view.DisplayName);
        Assert.Equal("/kaleido/queryable/functional-records/query", record.QueryUrl);
        Assert.Equal("/kaleido/queryable/functional-records/grid/query", view.QueryUrl);
        Assert.Single(view.Parameters!);
        Assert.NotEmpty(view.OutputFields);
    }
}
