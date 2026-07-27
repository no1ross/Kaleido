using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;
using System.Net;
using System.Net.Http.Json;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Discovery;

public sealed class QueryableDiscoveryTests
    : IClassFixture<QueryableAspNetCoreFixture>
{
    private readonly HttpClient _client;

    public QueryableDiscoveryTests(
        QueryableAspNetCoreFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetQueryable_Should_Return_Ok()
    {
        var response =
            await _client.GetAsync(
                "/kaleido/queryable");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task GetQueryable_Should_Return_Registered_Records()
    {
        var records =
            await _client.GetFromJsonAsync<RecordSummaryContract[]>(
                "/kaleido/queryable");

        Assert.NotNull(records);

        Assert.Contains(
            records,
            r => r.Name == "functional-records");
    }

    [Fact]
    public async Task GetQueryable_Should_Return_Record_Summary()
    {
        var records =
            await _client.GetFromJsonAsync<RecordSummaryContract[]>(
                "/kaleido/queryable");

        Assert.NotNull(records);

        var record =
            Assert.Single(records, r => r.Name == "functional-records");

        Assert.Equal(
            "functional-records",
            record.Name);

        Assert.False(
            string.IsNullOrWhiteSpace(
                record.MetadataUrl));

        Assert.False(
            string.IsNullOrWhiteSpace(
                record.QueryUrl));
    }

    [Fact]
    public async Task GetQueryable_Should_Return_Named_Query_Summaries()
    {
        var records =
            await _client.GetFromJsonAsync<RecordSummaryContract[]>(
                "/kaleido/queryable");

        Assert.NotNull(records);

        var record =
            Assert.Single(records, r => r.Name == "functional-records");

        Assert.Equal(
            4,
            record.NamedQueries.Count);

        var queryNames =
            record.NamedQueries
                .Select(q => q.Name)
                .ToArray();

        Assert.Equal(
            new[]
            {
            "active-records",
            "effective-on",
            "high-amount-records",
            "records-by-category"
            },
            queryNames);

        Assert.Equal(
            "/kaleido/queryable/functional-records/metadata",
            record.MetadataUrl);

        Assert.Equal(
            "/kaleido/queryable/functional-records/query",
            record.QueryUrl);
    }
}