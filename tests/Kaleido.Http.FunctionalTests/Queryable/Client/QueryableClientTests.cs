using Kaleido.Http.FunctionalTests.Queryable.Fixtures;
using Kaleido.Http.FunctionalTests.Queryable.Infrastructure;
using Kaleido.Http.Queryable;

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
        var registry = await _factory.GetClient("test").GetRegistryAsync(TestContext.Current.CancellationToken);

        Assert.Contains(registry, r => r.Name == "FunctionalRecordContextSource");
    }

    [Fact]
    public async Task GetRegistryAsync_ContainsExpectedQueryUrls()
    {
        var registry = await _factory.GetClient("test").GetRegistryAsync(TestContext.Current.CancellationToken);

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
                new QueryApiBody()), TestContext.Current.CancellationToken);

        // The view's default page (3) in its default order, with the total of all its rows.
        Assert.Equal(6, result.TotalCount);
        Assert.Equal([1, 2, 3], result.Results.Select(x => x.Id));
    }

    // ---------------------------------------------------------------------------
    // QuerySourceAsync
    // ---------------------------------------------------------------------------

    // The typed client and the server must agree on the JSON of every value type: a record read
    // back through the client equals the server's record field by field.
    [Fact]
    public async Task QuerySourceAsync_ReturnsTheRecordsTheSourceHolds_ValueForValue()
    {
        var result = await _factory.GetClient("test").QuerySourceAsync<FunctionalRecordContext>(
            "FunctionalRecordContextSource",
            new QueryApiRequest(new QueryApiBody { Page = new QueryApiPage { Size = 10 } }), TestContext.Current.CancellationToken);

        var expected = new FunctionalRecordData().Records.OrderBy(x => x.Id).ToArray();
        Assert.Equal(6, result.TotalCount);
        Assert.Equal(expected.Length, result.Results.Count);

        foreach (var (actual, record) in result.Results.Zip(expected))
        {
            Assert.Equal(record.Id, actual.Id);
            Assert.Equal(record.Code, actual.Code);
            Assert.Equal(record.Name, actual.Name);
            Assert.Equal(record.IsActive, actual.IsActive);
            Assert.Equal(record.Amount, actual.Amount);
            Assert.Equal(record.Status, actual.Status);
            Assert.Equal(record.EffectiveDate, actual.EffectiveDate);
            Assert.Equal(record.NullableScore, actual.NullableScore);
            Assert.Equal(record.ExternalId, actual.ExternalId);
            Assert.Equal(record.CreatedAt, actual.CreatedAt.ToUniversalTime());
        }
    }

    [Fact]
    public async Task QuerySourceAsync_WhenTheServiceRefusesTheQuery_ThrowsWithTheServicesErrors()
    {
        var body = new QueryApiBody
        {
            Filter = new QueryApiFilterNode
            {
                Condition = new QueryApiFilterCondition { Field = "Region", Operator = "equals", Values = [System.Text.Json.JsonSerializer.SerializeToElement("East")] },
            },
        };

        var exception =
            await Assert.ThrowsAsync<Kaleido.Http.Client.KaleidoHttpClientException>(() =>
                _factory.GetClient("test").QuerySourceAsync<FunctionalRecordContext>("FunctionalRecordContextSource", new QueryApiRequest(body), TestContext.Current.CancellationToken));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains(exception.Errors, x => x.Code == "qry_field_not_filterable");
    }
}
