using System.Net;
using System.Text.Json;
using Kaleido.Http.FunctionalTests.Queryable.Fixtures;
using Kaleido.Http.FunctionalTests.Queryable.Infrastructure;
using Kaleido.Http.Queryable;

namespace Kaleido.Http.FunctionalTests.Queryable.Execution;

public sealed class DelegatedQuerySourceTests : IClassFixture<QueryableAspNetCoreFixture>
{
    private const string QueryUrl = "/kaleido/queryable/functionalrecordsummarysource/query";

    private readonly HttpClient _client;
    private readonly IKaleidoQueryableClientFactory _factory;

    public DelegatedQuerySourceTests(QueryableAspNetCoreFixture fixture)
    {
        _client = fixture.Client;
        _factory = fixture.ClientFactory;
    }

    [Fact]
    public async Task Registry_PublishesDelegatedSourceLikeAnyOtherSource()
    {
        var registry = await _factory.GetClient("test").GetRegistryAsync();

        var source = Assert.Single(registry, r => r.Name == nameof(FunctionalRecordSummarySource));

        Assert.Equal(QueryUrl, source.QueryUrl);
        Assert.Empty(source.Views);
        Assert.Equal(nameof(FunctionalRecordSummaryParameters.Category), Assert.Single(source.Parameters).Name);
        Assert.Equal(nameof(FunctionalRecordSummary.Label), Assert.Single(source.OutputFields).Name);
        Assert.Contains(source.Fields, f => f.Name == nameof(FunctionalRecordContext.Code) && f.IsFilterable);
    }

    [Fact]
    public async Task Registry_DoesNotPublishHowTheSourceIsFulfilled()
    {
        var json = await _client.GetStringAsync("/kaleido/registry");

        using var document = JsonDocument.Parse(json);
        var source =
            document.RootElement.GetProperty("queryables")
                .EnumerateArray()
                .Single(x => x.GetProperty("name").GetString() == nameof(FunctionalRecordSummarySource));

        Assert.False(source.TryGetProperty("kind", out _));
    }

    [Fact]
    public async Task Query_ReturnsSourceResultsWithDownstreamPagingUntouched()
    {
        var result =
            await _factory.GetClient("test")
                .QuerySourceAsync<FunctionalRecordSummaryParameters, FunctionalRecordSummary>(
                    nameof(FunctionalRecordSummarySource),
                    new QueryApiRequest<FunctionalRecordSummaryParameters>(
                        new FunctionalRecordSummaryParameters { Category = "Alpha" },
                        new QueryApiBody { Page = new QueryApiPage { Size = 2, Offset = 4 } }));

        Assert.Equal(FunctionalRecordSummarySource.DownstreamTotalCount, result.TotalCount);
        Assert.Equal(4, result.Offset);
        Assert.Equal(["AL-001 (East)", "AL-004 (Central)"], result.Results.Select(x => x.Label));
    }

    [Fact]
    public async Task Query_WhenFilteringOnFieldNotInThePublicContract_ReturnsBadRequest()
    {
        var response =
            await _client.PostAsJsonAsync(
                QueryUrl,
                new
                {
                    parameters = new { category = "Alpha" },
                    query = new
                    {
                        filter = new { condition = new { field = nameof(FunctionalRecordContext.Region), @operator = "equals", values = new[] { "East" } } }
                    }
                });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("qry_field_not_filterable", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Query_WhenPageSizeExceedsSourceMaximum_ReturnsBadRequest()
    {
        var response =
            await _client.PostAsJsonAsync(
                QueryUrl,
                new
                {
                    parameters = new { category = "Alpha" },
                    query = new { page = new { size = 50, offset = 0 } }
                });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("qry_invalid_page_size", await response.Content.ReadAsStringAsync());
    }
}
