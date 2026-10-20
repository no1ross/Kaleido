using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kaleido.Http.FunctionalTests.Queryable.Fixtures;
using Kaleido.Http.FunctionalTests.Queryable.Infrastructure;

namespace Kaleido.Http.FunctionalTests.Queryable.Execution;

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
        // Page not provided: the default page (3 rows), and the total of all matching rows, so a
        // consumer knows there is more than this page (#343).
        Assert.Equal(6, GetTotalCount(root));
        Assert.Equal(0, GetOffset(root));
        Assert.Equal(3, GetPageSize(root));
        Assert.Equal(3, GetRecords(root).Length);
    }

    [Fact]
    public async Task PostDirectQuery_WithoutSort_PagesInTheSourcesDefaultOrder()
    {
        var first = await PostContextQueryForRecordsAsync(new QueryRequest(new QueryBody(Page: new QueryPage(3, 0))));
        var second = await PostContextQueryForRecordsAsync(new QueryRequest(new QueryBody(Page: new QueryPage(3, 3))));

        Assert.Equal([1, 2, 3], first.Select(x => GetInt32(x, "Id")));
        Assert.Equal([4, 5, 6], second.Select(x => GetInt32(x, "Id")));
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
    public async Task PostDirectQuery_WhenThePageIsPartial_ReportsOffsetPlusReturnedRows()
    {
        var request = new QueryRequest(new QueryBody(
            Sort: [new QuerySort("Id", SortDirection.Ascending)],
            Page: new QueryPage(4, 4)));

        var response = await PostContextQueryAsync(request);

        await AssertStatusCodeAsync(HttpStatusCode.OK, response);
        var root = await ReadResponseRootAsync(response);
        Assert.Equal(6, GetTotalCount(root));
        Assert.Equal(4, GetOffset(root));
        Assert.Equal(4, GetPageSize(root));
        Assert.Equal([5, 6], GetRecords(root).Select(x => GetInt32(x, "Id")));
    }

    [Fact]
    public async Task PostDirectQuery_WhenOffsetExceedsResults_ReturnsAnEmptyPage_AndTheTotal()
    {
        var request = new QueryRequest(new QueryBody(
            Sort: [new QuerySort("Id", SortDirection.Ascending)],
            Page: new QueryPage(4, 100)));

        var response = await PostContextQueryAsync(request);

        await AssertStatusCodeAsync(HttpStatusCode.OK, response);
        var root = await ReadResponseRootAsync(response);
        Assert.Equal(6, GetTotalCount(root));
        Assert.Equal(100, GetOffset(root));
        Assert.Equal(4, GetPageSize(root));
        Assert.Empty(GetRecords(root));
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
        // Page not provided: the view's default page (3 rows) and the total of all its rows.
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

    [Fact]
    public async Task PostDirectQuery_WithAPage_OnASourceThatDoesNotPage_Returns400_PagingNotSupported()
    {
        using var content = new StringContent("{\"query\":{\"page\":{\"size\":50,\"offset\":0}}}", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/kaleido/queryable/functionalunpagedrecordsource/query", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("qry_paging_not_supported", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // TotalCount is always the number of matching rows, whatever page was returned (#291). Six records,
    // both capabilities pageable (default 3). A partial page ends the results (offset + returned); a
    // full page or an empty page past the start is counted; an empty result has a total of 0.
    public static TheoryData<bool, int?, int?, bool, int, int> TotalCountCases { get; } =
        new()
        {
            // view?, page size, page offset, filter to nothing?, returned, total
            { false, null, null, false, 3, 6 },   // page omitted: the default page
            { false, 2, 0, false, 2, 6 },         // first page
            { false, 2, 2, false, 2, 6 },         // middle page
            { false, 2, 4, false, 2, 6 },         // final page, exactly full
            { false, 4, 4, false, 2, 6 },         // final page, partial
            { false, 2, 10, false, 0, 6 },        // beyond the end
            { false, null, null, true, 0, 0 },    // no matching rows
            { true, null, null, false, 3, 6 },
            { true, 2, 0, false, 2, 6 },
            { true, 2, 2, false, 2, 6 },
            { true, 2, 4, false, 2, 6 },
            { true, 4, 4, false, 2, 6 },
            { true, 2, 10, false, 0, 6 },
            { true, null, null, true, 0, 0 },
        };

    [Theory]
    [MemberData(nameof(TotalCountCases))]
    public async Task TotalCount_IsTheNumberOfMatchingRows_OnEveryPage(bool view, int? size, int? offset, bool filterToNothing, int returned, int total)
    {
        var body = new QueryBody(
            Filter: filterToNothing ? QueryFilterNode.CreateCondition("Category", FilterOperator.Equals, "NoSuchCategory") : null,
            Page: size is null ? null : new QueryPage(size.Value, offset ?? 0));

        var response = view
            ? await PostViewQueryAsync(new QueryRequest<FunctionalRecordViewParameters>(new FunctionalRecordViewParameters { Category = "Any" }, body))
            : await PostContextQueryAsync(new QueryRequest(body));

        await AssertStatusCodeAsync(HttpStatusCode.OK, response);
        var root = await ReadResponseRootAsync(response);
        Assert.Equal(returned, GetRecords(root).Length);
        Assert.Equal(total, GetTotalCount(root));
        Assert.Equal(offset ?? 0, GetOffset(root));
    }

    private Task<HttpResponseMessage> PostContextQueryAsync(QueryRequest request) =>
        PostJsonAsync("/kaleido/queryable/functionalrecordcontextsource/query", request);

    private async Task<JsonElement[]> PostContextQueryForRecordsAsync(QueryRequest request)
    {
        var response = await PostContextQueryAsync(request);
        await AssertStatusCodeAsync(HttpStatusCode.OK, response);
        return await ReadRecordsAsync(response);
    }

    // The wire contract names the view's parameters "parameters" (QueryApiRequest), not the core
    // request's "viewParameters".
    private Task<HttpResponseMessage> PostViewQueryAsync(QueryRequest<FunctionalRecordViewParameters> request) =>
        PostJsonAsync(
            "/kaleido/queryable/functionalrecordcontextsource/functionalrecordgridview/query",
            new { parameters = request.ViewParameters, query = request.Query });

    private Task<HttpResponseMessage> PostJsonAsync<T>(string url, T request) where T : class =>
        _client.PostAsJsonAsync(url, request, JsonOptions);

    private async Task<HttpResponseMessage> PostRawContextQueryAsync(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/kaleido/queryable/functionalrecordcontextsource/query", content);
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
        root.GetProperty("results").EnumerateArray().Select(x => x.Clone()).ToArray();

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
