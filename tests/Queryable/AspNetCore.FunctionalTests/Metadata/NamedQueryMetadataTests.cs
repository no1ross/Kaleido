using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;
using Kaleido.Queryable.Shared;
using System.Net;
using System.Net.Http.Json;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Metadata;

public sealed class NamedQueryMetadataTests
    : IClassFixture<QueryableAspNetCoreFixture>
{
    private readonly HttpClient _client;

    public NamedQueryMetadataTests(
        QueryableAspNetCoreFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetNamedQueryMetadata_Should_Return_NotFound_For_Unknown_Query()
    {
        var response =
            await _client.GetAsync(
                "/kaleido/queryable/functional-records/queries/unknown/metadata");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task ActiveRecords_Should_Return_Metadata()
    {
        var metadata =
            await _client.GetFromJsonAsync<NamedQueryContract>(
                "/kaleido/queryable/functional-records/queries/active-records/metadata");

        Assert.NotNull(metadata);

        Assert.Equal(
            "active-records",
            metadata.Name);

        Assert.Equal(
            "Returns only active records.",
            metadata.Description);

        Assert.Empty(
            metadata.Parameters);
    }

    [Fact]
    public async Task RecordsByCategory_Should_Return_Metadata()
    {
        var metadata =
            await _client.GetFromJsonAsync<NamedQueryContract>(
                "/kaleido/queryable/functional-records/queries/records-by-category/metadata");

        Assert.NotNull(metadata);

        Assert.Equal(
            "records-by-category",
            metadata.Name);

        Assert.Equal(
            "Returns records by category.",
            metadata.Description);

        var parameter =
            Assert.Single(metadata.Parameters);

        Assert.Equal(
            nameof(SampleKaleidoRecord.Category),
            parameter.Name);

        Assert.Equal(
            "string",
            parameter.DataType.Type);

        Assert.Null(
            parameter.DataType.Format);

        Assert.False(
            parameter.DataType.Nullable);

        Assert.True(
            parameter.Required);

        Assert.Equal(
            "The category to filter records by.",
            parameter.Description);

        Assert.Null(
            parameter.DefaultValue);
    }

    [Fact]
    public async Task HighAmountRecords_Should_Return_Metadata()
    {
        var metadata =
            await _client.GetFromJsonAsync<NamedQueryContract>(
                "/kaleido/queryable/functional-records/queries/high-amount-records/metadata");

        Assert.NotNull(metadata);

        Assert.Equal(
            "high-amount-records",
            metadata.Name);

        Assert.Equal(
            "Returns records with amounts above a threshold.",
            metadata.Description);

        var parameter =
            Assert.Single(metadata.Parameters);

        Assert.Equal(
            nameof(SampleKaleidoRecord.Amount),
            parameter.Name);

        Assert.Equal(
            "number",
            parameter.DataType.Type);

        Assert.Equal(
            "decimal",
            parameter.DataType.Format);

        Assert.False(
            parameter.DataType.Nullable);

        Assert.False(
            parameter.Required);

        Assert.Equal(
            "Minimum amount that a record must have.",
            parameter.Description);

        Assert.Equal(
            "100",
            parameter.DefaultValue!.ToString());
    }

    [Fact]
    public async Task EffectiveOn_Should_Return_Metadata()
    {
        var metadata =
            await _client.GetFromJsonAsync<NamedQueryContract>(
                "/kaleido/queryable/functional-records/queries/effective-on/metadata");

        Assert.NotNull(metadata);

        Assert.Equal(
            "effective-on",
            metadata.Name);

        Assert.Equal(
            "Returns records effective on a specific date.",
            metadata.Description);

        var parameter =
            Assert.Single(metadata.Parameters);

        Assert.Equal(
            nameof(SampleKaleidoRecord.EffectiveDate),
            parameter.Name);

        Assert.Equal(
            "string",
            parameter.DataType.Type);

        Assert.Equal(
            "date",
            parameter.DataType.Format);

        Assert.False(
            parameter.DataType.Nullable);

        Assert.True(
            parameter.Required);

        Assert.Equal(
            "The date to filter records by.",
            parameter.Description);

        Assert.Null(
            parameter.DefaultValue);
    }
}