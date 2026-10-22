using System.Net;
using Kaleido.Http.FunctionalTests.Queryable.Fixtures;
using Kaleido.Http.FunctionalTests.Queryable.Infrastructure;
using Kaleido.Http.Queryable;
using Kaleido.Http.Registry;

namespace Kaleido.Http.FunctionalTests.Queryable.Discovery;

public sealed class QueryableDiscoveryTests(QueryableAspNetCoreFixture fixture) : IClassFixture<QueryableAspNetCoreFixture>
{
    private readonly HttpClient _client = fixture.Client;

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

        var record = Assert.Single(registry!.Queryables, x => x.Name == "FunctionalRecordContextSource");

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
        var record = Assert.Single(registry!.Queryables, x => x.Name == "FunctionalRecordContextSource");
        var view = Assert.Single(record.Views, x => x.Name == "FunctionalRecordGridView");

        Assert.Equal("/kaleido/queryable/functionalrecordcontextsource/query", record.QueryUrl);
        Assert.Equal("Grid View", view.DisplayName);
        Assert.Equal("/kaleido/queryable/functionalrecordcontextsource/functionalrecordgridview/query", view.QueryUrl);
        Assert.Single(view.Parameters!);
        Assert.NotEmpty(view.OutputFields);
    }

    // What a consumer (or an agent) builds its queries from: the published field metadata must be
    // exactly what the attributes declare, since the runtime enforces those same declarations.
    [Fact]
    public async Task GetRegistry_PublishesEachFieldsDeclaredCapabilities()
    {
        var registry = await _client.GetFromJsonAsync<AggregatedRegistryResponse>("/kaleido/registry", KaleidoJsonOptions.Options);
        var record = Assert.Single(registry!.Queryables, x => x.Name == "FunctionalRecordContextSource");
        QueryableFieldMetadata Field(string name) => Assert.Single(record.Fields, x => x.Name == name);

        var name = Field("Name");
        Assert.Equal("The record's display name.", name.Description);
        Assert.Equal([FunctionalSensitiveDataAttribute.Sensitive.ToString()], name.Classifications);
        Assert.True(name.IsSearchable);

        var amount = Field("Amount");
        Assert.Equal(
            [FilterOperator.Equals, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.Between],
            amount.FilterOperators);
        Assert.True(amount.IsSortable);

        var region = Field("Region");
        Assert.False(region.IsFilterable);
        Assert.False(region.IsSortable);
        Assert.True(region.IsSearchable);

        Assert.Equal(3, record.Pageable!.DefaultSize);
        Assert.Equal(10, record.Pageable.MaxSize);
    }

    [Fact]
    public async Task GetRegistry_PublishesAViewsOwnPaging_NotItsSources()
    {
        var registry = await _client.GetFromJsonAsync<AggregatedRegistryResponse>("/kaleido/registry", KaleidoJsonOptions.Options);
        var source = Assert.Single(registry!.Queryables, x => x.Name == "FunctionalAsyncRecordSource");
        var view = Assert.Single(source.Views, x => x.Name == "FunctionalCodeListView");

        Assert.Equal(10, source.Pageable!.MaxSize);
        Assert.Equal(2, view.Pageable!.DefaultSize);
        Assert.Equal(4, view.Pageable.MaxSize);
    }
}
