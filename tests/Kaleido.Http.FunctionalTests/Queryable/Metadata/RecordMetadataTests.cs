using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;
using System.Net;
using System.Net.Http.Json;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Metadata;

public sealed class RecordMetadataTests : IClassFixture<QueryableAspNetCoreFixture>
{
    private readonly HttpClient _client;

    public RecordMetadataTests(QueryableAspNetCoreFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetRecordMetadata_ReturnsOk()
    {
        var response = await _client.GetAsync("/kaleido/queryable/functional-records/metadata");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRecordMetadata_ReturnsContextMetadataAndViews()
    {
        var metadata = await _client.GetFromJsonAsync<QueryableRecordResponse>("/kaleido/queryable/functional-records/metadata");

        Assert.NotNull(metadata);
        Assert.Equal("functional-records", metadata.Name);
        Assert.Equal("Functional records for Queryable HTTP tests.", metadata.Description);
        Assert.Equal("Functional Records", metadata.DisplayName);
        Assert.Equal("1.0.0", metadata.Version);
        Assert.Equal("AspNetCore Functional Test Data", metadata.Source);
        Assert.Equal("/kaleido/queryable/functional-records/metadata", metadata.MetadataUrl);
        Assert.Equal("/kaleido/queryable/functional-records/query", metadata.QueryUrl);

        Assert.Contains(metadata.Fields, x => x.Name == "Id");
        Assert.Contains(metadata.Fields, x => x.Name == "Code");
        Assert.Contains(metadata.Fields, x => x.Name == "Amount");
        Assert.Contains(metadata.Fields, x => x.Name == "NullableScore");

        var view = Assert.Single(metadata.Views, x => x.Name == "grid");
        Assert.Equal("Grid view for functional records.", view.Description);
        Assert.NotNull(view.Pageable);
        Assert.Equal(3, view.Pageable.DefaultSize);
        Assert.Equal(10, view.Pageable.MaxSize);
        Assert.NotEmpty(view.OutputFields);
    }

    [Fact]
    public async Task GetRecordMetadata_ReturnsNotFoundForUnknownRecord()
    {
        var response = await _client.GetAsync("/kaleido/queryable/unknown-record/metadata");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
