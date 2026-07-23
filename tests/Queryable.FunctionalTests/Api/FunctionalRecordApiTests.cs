using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kaleido.FunctionalTests.Fixtures;
using Kaleido.FunctionalTests.Infrastructure;
using Kaleido.Queryable;
using Kaleido.Shared;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.FunctionalTests.Api;

public sealed class FunctionalRecordApiTests : IClassFixture<FunctionalApiFixture>
{
    private readonly FunctionalApiFixture _fixture;
    private readonly JsonSerializerOptions _jsonOptions;

    public FunctionalRecordApiTests(FunctionalApiFixture fixture)
    {
        _fixture = fixture;
        _jsonOptions = JsonOptions.Create();
    }

    [Fact]
    public async Task GetRecords_Should_Return_Functional_Record_Metadata()
    {
        var client = _fixture.Client;
        var records = await client.GetFromJsonAsync<IReadOnlyList<RecordDescriptor>>("/v1/records", _jsonOptions);

        Assert.NotNull(records);
        var record = Assert.Single(records!, x => x.Key == "functional-records");
        Assert.Equal("functional-records", record.Key);
        Assert.Equal("SampleKaleidoRecord", record.Name);
        Assert.Equal("1.0.0", record.Version);
        Assert.Equal("CSV Functional Test Data", record.Source);
        Assert.NotEmpty(record.Fields);
    }

    //[Fact]
    //public async Task GetRecordMetadata_Should_Return_Functional_Record_Metadata()
    //{
    //    var client = _fixture.Client;
    //    var record = await client.GetFromJsonAsync<RecordDescriptor>("/v1/records/functional-records", _jsonOptions);

    //    Assert.NotNull(record);
    //    Assert.Equal("functional-records", record!.Name);
    //    Assert.NotEmpty(record.Fields);
    //}

    [Theory]
    [MemberData(nameof(FunctionalScenarios.All), MemberType = typeof(FunctionalScenarios))]
    public async Task Api_Query_Should_Produce_Expected_Result(FunctionalScenario scenario)
    {
        using var scope = _fixture.Services.CreateScope();
        var data = scope.ServiceProvider.GetRequiredService<SampleKaleidoCsvData>();
        var request = scenario.CreateRequest(data.Records);
        var client = _fixture.Client;

        var response = await client.PostAsJsonAsync(
            "/v1/records/functional-records/query",
            request,
            _jsonOptions);

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        Console.WriteLine(json);

        var content = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != HttpStatusCode.OK) 
        { 
            throw new Xunit.Sdk.XunitException($"Status Code: {response.StatusCode} Response: {content}");
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var apiResponse = JsonSerializer.Deserialize<ApiFunctionalQueryResponse>(content, _jsonOptions);
        Assert.NotNull(apiResponse);

        FunctionalAssertions.AssertScenarioResult(
            scenario,
            data.Records,
            new FunctionalQueryResult(apiResponse!.Items, apiResponse.TotalCount));
    }

    private sealed record ApiFunctionalQueryResponse(
        RecordDescriptor Descriptor,
        int TotalCount,
        IReadOnlyList<SampleKaleidoRecord> Items);
}
