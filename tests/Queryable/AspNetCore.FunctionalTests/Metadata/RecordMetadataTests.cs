using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;
using System.Net;
using System.Net.Http.Json;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Metadata;

public sealed class RecordMetadataTests
    : IClassFixture<QueryableAspNetCoreFixture>
{
    private readonly HttpClient _client;

    public RecordMetadataTests(
        QueryableAspNetCoreFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetRecordMetadata_Should_Return_Ok()
    {
        var response =
            await _client.GetAsync(
                "/kaleido/queryable/functional-records/metadata");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task GetRecordMetadata_Should_Return_Record_Metadata()
    {
        var metadata =
            await _client.GetFromJsonAsync<RecordContract>(
                "/kaleido/queryable/functional-records/metadata");

        Assert.NotNull(metadata);

        Assert.Equal(
            "functional-records",
            metadata.Name);

        Assert.Equal(
            "SampleKaleidoRecord",
            metadata.Description);

        Assert.Equal(
            "1.0.0",
            metadata.Version);

        Assert.Equal(
            "CSV Functional Test Data",
            metadata.Source);
    }

    [Fact]
    public async Task GetRecordMetadata_Should_Return_Pageable_Metadata()
    {
        var metadata =
            await _client.GetFromJsonAsync<RecordContract>(
                "/kaleido/queryable/functional-records/metadata");

        Assert.NotNull(metadata);
        Assert.NotNull(metadata.Pageable);

        Assert.Equal(
            25,
            metadata.Pageable.DefaultSize);

        Assert.Equal(
            500,
            metadata.Pageable.MaxSize);
    }

    [Fact]
    public async Task GetRecordMetadata_Should_Return_Record_Fields()
    {
        var metadata =
            await _client.GetFromJsonAsync<RecordContract>(
                "/kaleido/queryable/functional-records/metadata");

        Assert.NotNull(metadata);

        Assert.Contains(
            metadata.Fields,
            f => f.Name == "Id");

        Assert.Contains(
            metadata.Fields,
            f => f.Name == "Code");

        Assert.Contains(
            metadata.Fields,
            f => f.Name == "Name");

        Assert.Contains(
            metadata.Fields,
            f => f.Name == "Amount");

        Assert.Contains(
            metadata.Fields,
            f => f.Name == "EffectiveDate");

        Assert.Contains(
            metadata.Fields,
            f => f.Name == "NullableScore");
    }

    [Fact]
    public async Task GetRecordMetadata_Should_Return_All_Named_Query_Summaries()
    {
        var metadata =
            await _client.GetFromJsonAsync<RecordContract>(
                "/kaleido/queryable/functional-records/metadata");

        Assert.NotNull(metadata);

        Assert.Equal(
            4,
            metadata.NamedQueries.Count);

        var queryNames =
            metadata.NamedQueries
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
    }

    [Fact]
    public async Task GetRecordMetadata_Should_Return_NotFound_For_Unknown_Record()
    {
        var response =
            await _client.GetAsync(
                "/kaleido/queryable/unknown-record/metadata");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
}