//using System.Net;
//using System.Net.Http.Json;
//using Kaleido.Queryable.AspNetCore.Contracts;
//using Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;

//namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Metadata;

//public sealed class MetadataEndpointTests
//    : IClassFixture<QueryableAspNetCoreFixture>
//{
//    private readonly HttpClient _client;

//    public MetadataEndpointTests(
//        QueryableAspNetCoreFixture fixture)
//    {
//        _client = fixture.Client;
//    }

//    [Fact]
//    public async Task GetMetadata_Should_Return_Ok()
//    {
//        var response =
//            await _client.GetAsync(
//                "/kaleido/queryable/functional-records/metadata");

//        Assert.Equal(
//            HttpStatusCode.OK,
//            response.StatusCode);
//    }

//    [Fact]
//    public async Task GetMetadata_Should_Return_Record_Metadata()
//    {
//        var metadata =
//            await _client.GetFromJsonAsync<RecordContract>(
//                "/kaleido/queryable/functional-records/metadata");

//        Assert.NotNull(metadata);

//        Assert.Equal(
//            "functional-records",
//            metadata.Name);

//        Assert.NotNull(metadata.Fields);
//        Assert.NotEmpty(metadata.Fields);

//        Assert.NotNull(metadata.NamedQueries);

//        Assert.False(
//            string.IsNullOrWhiteSpace(metadata.MetadataUrl));

//        Assert.False(
//            string.IsNullOrWhiteSpace(metadata.QueryUrl));
//    }

//    [Fact]
//    public async Task GetMetadata_Should_Return_Metadata_Url()
//    {
//        var metadata =
//            await _client.GetFromJsonAsync<RecordContract>(
//                "/kaleido/queryable/functional-records/metadata");

//        Assert.NotNull(metadata);

//        Assert.Equal(
//            "/kaleido/queryable/functional-records/metadata",
//            metadata.MetadataUrl);
//    }

//    [Fact]
//    public async Task GetMetadata_Should_Return_Query_Url()
//    {
//        var metadata =
//            await _client.GetFromJsonAsync<RecordContract>(
//                "/kaleido/queryable/functional-records/metadata");

//        Assert.NotNull(metadata);

//        Assert.Equal(
//            "/kaleido/queryable/functional-records/query",
//            metadata.QueryUrl);
//    }

//    [Fact]
//    public async Task GetMetadata_Should_Return_NotFound_For_Unknown_Record()
//    {
//        var response =
//            await _client.GetAsync(
//                "/kaleido/queryable/does-not-exist/metadata");

//        Assert.Equal(
//            HttpStatusCode.NotFound,
//            response.StatusCode);
//    }
//}