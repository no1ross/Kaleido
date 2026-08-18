using Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Infrastructure;
using Kaleido.Queryable.Query;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Execution;

public sealed class QueryExecutionTests : IClassFixture<QueryableAspNetCoreFixture>
{
    private readonly HttpClient _client;

    public QueryExecutionTests(QueryableAspNetCoreFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task PostDirectQuery_ReturnsDefaultPage()
    {
        var response = await PostContextQueryAsync(new QueryRequest(new QueryBody()));

        await AssertStatusCodeAsync(HttpStatusCode.OK, response);

        var root = await ReadResponseRootAsync(response);
        Assert.Equal(6, GetTotalCount(root));
        Assert.Equal(0, GetOffset(root));
        Assert.Equal(3, GetPageSize(root));
        Assert.Equal(3, GetRecords(root).Length);
    }

    [Fact]
    public async Task PostDirectQuery_FiltersByCategory()
    {
        var request = new QueryRequest(new QueryBody(
            Filter: QueryFilterNode.CreateCondition("Category", FilterOperator.Equals, "Alpha")));

        var records = await PostContextQueryForRecordsAsync(request);

        Assert.Equal([1, 4], records.Select(x => GetInt32(x, "Id")));
    }

    [Fact]
    public async Task PostDirectQuery_SearchesAndSorts()
    {
        var request = new QueryRequest(new QueryBody(
            SearchText: "Gamma",
            Sort: [new QuerySort("Amount", SortDirection.Descending)]));

        var records = await PostContextQueryForRecordsAsync(request);

        Assert.Equal([6, 3], records.Select(x => GetInt32(x, "Id")));
    }

    [Fact]
    public async Task PostDirectQuery_AppliesPaging()
    {
        var request = new QueryRequest(new QueryBody(
            Sort: [new QuerySort("Id", SortDirection.Ascending)],
            Page: new QueryPage(2, 2)));

        var response = await PostContextQueryAsync(request);

        await AssertStatusCodeAsync(HttpStatusCode.OK, response);

        var root = await ReadResponseRootAsync(response);
        Assert.Equal(6, GetTotalCount(root));
        Assert.Equal(2, GetOffset(root));
        Assert.Equal(2, GetPageSize(root));
        Assert.Equal([3, 4], GetRecords(root).Select(x => GetInt32(x, "Id")));
    }

    [Fact]
    public async Task PostDirectQuery_AcceptsRawJsonEnumFilter()
    {
        var response = await PostRawContextQueryAsync(
            """
            {
              "query": {
                "filter": {
                  "condition": {
                    "field": "Status",
                    "operator": "equals",
                    "values": [ "active" ]
                  },
                  "group": null
                }
              }
            }
            """);

        await AssertStatusCodeAsync(HttpStatusCode.OK, response);

        var records = await ReadRecordsAsync(response);
        Assert.Equal([1, 3, 6], records.Select(x => GetInt32(x, "Id")));
    }

    [Fact]
    public async Task PostViewQuery_ReturnsProjectedRecords()
    {
        var response = await PostViewQueryAsync(new QueryRequest<FunctionalRecordViewParameters>(
            new FunctionalRecordViewParameters { Category = "Alpha" },
            new QueryBody()));

        await AssertStatusCodeAsync(HttpStatusCode.OK, response);

        var root = await ReadResponseRootAsync(response);
        Assert.Equal(6, GetTotalCount(root));
        Assert.Equal(3, GetRecords(root).Length);
        Assert.Contains(GetRecords(root), x => GetString(x, "Code") == "AL-001");
    }

    [Fact]
    public async Task PostViewQuery_UsesFilterSortAndPaging()
    {
        var request = new QueryRequest<FunctionalRecordViewParameters>(
            new FunctionalRecordViewParameters { Category = "Gamma" },
            new QueryBody(
                Filter: QueryFilterNode.CreateCondition("IsActive", FilterOperator.IsTrue),
                Sort: [new QuerySort("Amount", SortDirection.Descending)],
                Page: new QueryPage(1, 0)));

        var response = await PostViewQueryAsync(request);

        await AssertStatusCodeAsync(HttpStatusCode.OK, response);

        var root = await ReadResponseRootAsync(response);
        Assert.Equal(4, GetTotalCount(root));
        var record = Assert.Single(GetRecords(root));
        Assert.Equal(6, GetInt32(record, "Id"));
    }

    [Fact]
    public async Task PostDirectQuery_ReturnsBadRequestForUnknownField()
    {
        var request = new QueryRequest(new QueryBody(
            Filter: QueryFilterNode.CreateCondition("DoesNotExist", FilterOperator.Equals, "anything")));

        var response = await PostContextQueryAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private Task<HttpResponseMessage> PostContextQueryAsync(QueryRequest request) =>
        PostJsonAsync("/kaleido/queryable/functional-records/query", request);

    private async Task<JsonElement[]> PostContextQueryForRecordsAsync(QueryRequest request)
    {
        var response = await PostContextQueryAsync(request);
        await AssertStatusCodeAsync(HttpStatusCode.OK, response);
        return await ReadRecordsAsync(response);
    }

    private Task<HttpResponseMessage> PostViewQueryAsync(QueryRequest<FunctionalRecordViewParameters> request) =>
        PostJsonAsync("/kaleido/queryable/functional-records/grid/query", request);

    private Task<HttpResponseMessage> PostJsonAsync<T>(string url, T request) where T : class =>
        _client.PostAsJsonAsync(url, request, JsonOptions);

    private async Task<HttpResponseMessage> PostRawContextQueryAsync(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/kaleido/queryable/functional-records/query", content);
    }

    private static async Task AssertStatusCodeAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        Assert.Fail($"Expected status code {expected}, but received {response.StatusCode}. Response body: {content}");
    }

    private static async Task<JsonElement> ReadResponseRootAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement[]> ReadRecordsAsync(HttpResponseMessage response)
    {
        var root = await ReadResponseRootAsync(response);
        return GetRecords(root);
    }

    private static JsonElement[] GetRecords(JsonElement root) =>
        root.GetProperty("records").EnumerateArray().Select(x => x.Clone()).ToArray();

    private static int GetTotalCount(JsonElement root) => root.GetProperty("totalCount").GetInt32();
    private static int GetOffset(JsonElement root) => root.GetProperty("offset").GetInt32();
    private static int GetPageSize(JsonElement root) => root.GetProperty("pageSize").GetInt32();
    private static int GetInt32(JsonElement element, string propertyName) => GetProperty(element, propertyName).GetInt32();
    private static string GetString(JsonElement element, string propertyName) => GetProperty(element, propertyName).GetString()!;

    private static JsonElement GetProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value))
        {
            return value;
        }

        var camel = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
        if (element.TryGetProperty(camel, out value))
        {
            return value;
        }

        throw new InvalidOperationException($"Property '{propertyName}' was not found.");
    }

    private sealed class LowerInvariantJsonNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) => name.ToLowerInvariant();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(new LowerInvariantJsonNamingPolicy(), allowIntegerValues: false)
        }
    };
}
