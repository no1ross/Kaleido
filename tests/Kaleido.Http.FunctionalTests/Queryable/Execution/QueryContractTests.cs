using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kaleido.Http.FunctionalTests.Queryable.Fixtures;
using Kaleido.Http.FunctionalTests.Queryable.Infrastructure;
using Kaleido.Http.Queryable;

namespace Kaleido.Http.FunctionalTests.Queryable.Execution;

// The query wire contract through the real pipeline: QueryApiBody JSON -> QueryBodyResolver (typed
// values) -> validator -> compiler -> applier -> executor -> QueryResult JSON. Each case asserts the
// exact records, so a wrong operator, value conversion, group, sort or paging rule fails here even
// when every component's own unit tests still pass.
public sealed class QueryContractTests(QueryableAspNetCoreFixture fixture) : IClassFixture<QueryableAspNetCoreFixture>
{
    private const string SourceUrl = "/kaleido/queryable/functionalrecordcontextsource/query";
    private const string AsyncSourceUrl = "/kaleido/queryable/functionalasyncrecordsource/query";
    private const string AsyncViewUrl = "/kaleido/queryable/functionalasyncrecordsource/functionalcodelistview/query";
    private const string GridViewUrl = "/kaleido/queryable/functionalrecordcontextsource/functionalrecordgridview/query";

    private readonly HttpClient _client = fixture.Client;

    public static TheoryData<string, string, int[]> Conditions() =>
        new()
        {
            { "Category", """ "operator":"notequals", "values":["Alpha"] """, [2, 3, 5, 6] },
            { "Code", """ "operator":"notcontains", "values":["AL"] """, [2, 3, 5, 6] },
            { "Code", """ "operator":"endswith", "values":["-006"] """, [6] },
            { "Code", """ "operator":"startswith", "values":["GA"] """, [3, 6] },
            { "Name", """ "operator":"contains", "values":["amma"] """, [3, 6] },
            { "Id", """ "operator":"in", "values":[2, 5] """, [2, 5] },
            { "Id", """ "operator":"notin", "values":[1, 2, 3] """, [4, 5, 6] },
            { "Amount", """ "operator":"between", "values":[30, 60] """, [3, 4] },
            { "Amount", """ "operator":"between", "values":[25.0, 40.0] """, [2, 3] },
            { "Amount", """ "operator":"lessthan", "values":[25] """, [1] },
            { "Amount", """ "operator":"greaterthanorequal", "values":[70] """, [5, 6] },
            { "EffectiveDate", """ "operator":"equals", "values":["2024-01-05"] """, [2] },
            { "EffectiveDate", """ "operator":"greaterthanorequal", "values":["2024-03-01"] """, [5, 6] },
            { "NullableScore", """ "operator":"isnull" """, [2, 5] },
            { "NullableScore", """ "operator":"isnotnull" """, [1, 3, 4, 6] },
            { "IsActive", """ "operator":"isfalse" """, [2, 5] },
            { "ExternalId", """ "operator":"equals", "values":["00000000-0000-0000-0000-000000000004"] """, [4] },
            { "CreatedAt", """ "operator":"greaterthanorequal", "values":["2024-01-04T00:00:00Z"] """, [4, 5, 6] },
            { "Status", """ "operator":"equals", "values":["Retired"] """, [5] },
        };

    [Theory]
    [MemberData(nameof(Conditions))]
    public async Task AFilterCondition_SelectsExactlyTheMatchingRecords(string field, string operatorAndValues, int[] expected)
    {
        var ids = await QueryIdsAsync($$"""{ "query": { "filter": { "condition": { "field":"{{field}}", {{operatorAndValues}} } }, "page": { "size": 10 } } }""");

        Assert.Equal(expected, ids);
    }

    [Fact]
    public async Task AFieldName_IsMatchedCaseInsensitively_ThroughValidationAndExecution()
    {
        var ids = await QueryIdsAsync("""{ "query": { "filter": { "condition": { "field":"category", "operator":"EQUALS", "values":["Alpha"] } } } }""");

        Assert.Equal([1, 4], ids);
    }

    [Fact]
    public async Task AnOrGroup_ReturnsTheUnionOfItsConditions()
    {
        var ids = await QueryIdsAsync(
            """
            { "query": { "page": { "size": 10 }, "filter": { "group": { "operator":"or", "filters": [
                { "condition": { "field":"Category", "operator":"equals", "values":["Alpha"] } },
                { "condition": { "field":"Category", "operator":"equals", "values":["Beta"] } } ] } } } }
            """);

        Assert.Equal([1, 2, 4], ids);
    }

    [Fact]
    public async Task AnAndGroup_ReturnsTheIntersectionOfItsConditions()
    {
        var ids = await QueryIdsAsync(
            """
            { "query": { "page": { "size": 10 }, "filter": { "group": { "operator":"and", "filters": [
                { "condition": { "field":"Category", "operator":"equals", "values":["Gamma"] } },
                { "condition": { "field":"Amount", "operator":"greaterthanorequal", "values":[50] } } ] } } } }
            """);

        Assert.Equal([6], ids);
    }

    [Fact]
    public async Task ANestedGroup_CombinesAndAndOrAsWritten()
    {
        // IsActive AND (Category = Alpha OR Category = Delta): Delta 5 is inactive, so only the Alphas.
        var ids = await QueryIdsAsync(
            """
            { "query": { "page": { "size": 10 }, "filter": { "group": { "operator":"and", "filters": [
                { "condition": { "field":"IsActive", "operator":"istrue" } },
                { "group": { "operator":"or", "filters": [
                    { "condition": { "field":"Category", "operator":"equals", "values":["Alpha"] } },
                    { "condition": { "field":"Category", "operator":"equals", "values":["Delta"] } } ] } } ] } } } }
            """);

        Assert.Equal([1, 4], ids);
    }

    [Fact]
    public async Task SeveralSorts_AreAppliedInOrder()
    {
        var ids = await QueryIdsAsync(
            """{ "query": { "page": { "size": 10 }, "sort": [ { "field":"Category", "direction":"ascending" }, { "field":"Amount", "direction":"descending" } ] } }""");

        Assert.Equal([4, 1, 2, 5, 6, 3], ids);
    }

    [Fact]
    public async Task AnExplicitSortSequence_WinsOverThePositionInTheList()
    {
        var ids = await QueryIdsAsync(
            """{ "query": { "page": { "size": 10 }, "sort": [ { "field":"Amount", "direction":"descending", "sequence": 2 }, { "field":"Category", "direction":"ascending", "sequence": 1 } ] } }""");

        Assert.Equal([4, 1, 2, 5, 6, 3], ids);
    }

    [Fact]
    public async Task SearchText_MatchesInsideAValue_AcrossTheSearchableFields()
    {
        // "est" is inside the Region "West" only; "amma" inside the Names "Gamma ...".
        var west = await QueryIdsAsync("""{ "query": { "searchText": "est" } }""");
        var gamma = await QueryIdsAsync("""{ "query": { "searchText": "amma" } }""");

        Assert.Equal([2], west);
        Assert.Equal([3, 6], gamma);
    }

    [Fact]
    public async Task SearchFilterSortAndPage_ComposeInOneRequest()
    {
        var root = await QueryAsync(
            """
            { "query": { "searchText": "a", "page": { "size": 2, "offset": 1 },
              "filter": { "condition": { "field":"IsActive", "operator":"istrue" } },
              "sort": [ { "field":"Amount", "direction":"descending" } ] } }
            """);

        // Active records, all containing "a" somewhere searchable: 6, 4, 3, 1 by amount descending.
        Assert.Equal(4, root.GetProperty("totalCount").GetInt32());
        Assert.Equal([4, 3], Ids(root));
    }

    [Fact]
    public async Task EnumValues_AreReturnedAsCamelCaseStrings_ThatAFilterAcceptsBack()
    {
        var root = await QueryAsync("""{ "query": { "filter": { "condition": { "field":"Id", "operator":"equals", "values":[4] } } } }""");

        var record = Assert.Single(root.GetProperty("results").EnumerateArray());
        Assert.Equal(JsonValueKind.String, record.GetProperty("status").ValueKind);
        Assert.Equal("suspended", record.GetProperty("status").GetString());

        var echoed = await QueryIdsAsync("""{ "query": { "filter": { "condition": { "field":"Status", "operator":"equals", "values":["suspended"] } } } }""");
        Assert.Equal([4], echoed);
    }

    public static TheoryData<string, string> RequestErrors() =>
        new()
        {
            { """{ "query": { "filter": { "condition": { "field":"Code", "operator":"greaterthan", "values":["A"] } } } }""", "qry_unsupported_operator" },
            { """{ "query": { "filter": { "condition": { "field":"Region", "operator":"equals", "values":["East"] } } } }""", "qry_field_not_filterable" },
            { """{ "query": { "sort": [ { "field":"Region", "direction":"ascending" } ] } }""", "qry_field_not_sortable" },
            { """{ "query": { "filter": { "group": { "operator":"and", "filters": [] } } } }""", "qry_empty_filter_group" },
            { """{ "query": { "page": { "size": 11 } } }""", "qry_invalid_page_size" },
            { """{ "query": { "page": { "offset": -1 } } }""", "qry_invalid_page_offset" },
            { """{ "query": { "filter": { } } }""", "qry_invalid_filter_node" },
            { """{ "query": { "filter": { "condition": { "field":"Code", "operator":"equals", "values":["A"] }, "group": { "operator":"and", "filters": [ { "condition": { "field":"Code", "operator":"equals", "values":["B"] } } ] } } } }""", "qry_invalid_filter_node" },
            { """{ "query": { "filter": { "group": { "operator":"and", "filters": [ { }, { "condition": { "field":"Code", "operator":"equals", "values":["A"] } } ] } } } }""", "qry_invalid_filter_node" },
        };

    [Theory]
    [MemberData(nameof(RequestErrors))]
    public async Task AnInvalidRequest_IsA400_NamingWhatIsWrong(string json, string code)
    {
        var response = await PostAsync(SourceUrl, json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("EffectiveDate", "\"not-a-date\"")]
    [InlineData("Amount", "\"lots\"")]
    [InlineData("ExternalId", "\"not-a-guid\"")]
    [InlineData("Status", "\"Unheard\"")]
    public async Task AValueThatDoesNotFitTheFieldsType_IsA400_NotAServerError(string field, string value)
    {
        var response = await PostAsync(SourceUrl, $$"""{ "query": { "filter": { "condition": { "field":"{{field}}", "operator":"equals", "values":[{{value}}] } } } }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnAsyncSource_IsAwaited_AndQueriedLikeAnySource()
    {
        var root = await QueryAsync(
            """{ "query": { "filter": { "condition": { "field":"Category", "operator":"equals", "values":["Gamma"] } } } }""",
            AsyncSourceUrl);

        Assert.Equal(2, root.GetProperty("totalCount").GetInt32());
        Assert.Equal([3, 6], Ids(root));
    }

    [Fact]
    public async Task AView_HasItsOwnDefaultPageAndOrder_NotTheSources()
    {
        // The source pages 3 rows by id; the async view pages 2 rows by code.
        var root = await QueryAsync("""{ "query": {} }""", AsyncViewUrl);

        Assert.Equal(6, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(["AL-001", "AL-004"], root.GetProperty("results").EnumerateArray().Select(x => x.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task AView_EnforcesItsOwnMaximumPageSize()
    {
        // 5 is within the source's maximum (10) but above the view's (4).
        var response = await PostAsync(AsyncViewUrl, """{ "query": { "page": { "size": 5 } } }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("qry_invalid_page_size", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AView_FiltersOnTheSourcesFields_BeforeProjecting()
    {
        // Amount is not part of the view's records: the filter runs on the query context.
        var root = await QueryAsync(
            """{ "query": { "filter": { "condition": { "field":"Amount", "operator":"greaterthanorequal", "values":[55] } } } }""",
            AsyncViewUrl);

        Assert.Equal(3, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(["AL-004", "DE-005"], root.GetProperty("results").EnumerateArray().Select(x => x.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task AView_WithoutItsRequiredParameter_IsA400()
    {
        var response = await PostAsync(GridViewUrl, """{ "query": {} }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("qry_missing_parameter", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // A delegated source forwards the consumer's query with ToApiBody(). The forwarded body must
    // reach the downstream source as the same query: typed values (enum, date, decimal), nested
    // groups, sort sequence and page survive the core -> wire -> core round trip.
    [Fact]
    public async Task AForwardedQuery_ReturnsWhatTheOriginalQueryReturns()
    {
        var original =
            """
            { "query": { "page": { "size": 10 },
              "filter": { "group": { "operator":"or", "filters": [
                { "group": { "operator":"and", "filters": [
                  { "condition": { "field":"Status", "operator":"equals", "values":["active"] } },
                  { "condition": { "field":"Amount", "operator":"greaterthanorequal", "values":[40.5] } } ] } },
                { "condition": { "field":"EffectiveDate", "operator":"equals", "values":["2024-01-05"] } } ] } },
              "sort": [ { "field":"Amount", "direction":"descending" } ] } }
            """;

        var forwardedBody =
            new QueryBody(
                Filter: new QueryFilterNode(
                    null,
                    new QueryFilterGroup(
                        LogicalOperator.Or,
                        [
                            new QueryFilterNode(
                                null,
                                new QueryFilterGroup(
                                    LogicalOperator.And,
                                    [
                                        QueryFilterNode.CreateCondition("Status", FilterOperator.Equals, FunctionalRecordStatus.Active),
                                        QueryFilterNode.CreateCondition("Amount", FilterOperator.GreaterThanOrEqual, 40.5m),
                                    ])),
                            QueryFilterNode.CreateCondition("EffectiveDate", FilterOperator.Equals, new DateOnly(2024, 1, 5)),
                        ])),
                Sort: [new QuerySort("Amount", SortDirection.Descending)],
                Page: new QueryPage(10, 0))
            .ToApiBody();

        var direct = await QueryIdsAsync(original);
        var forwarded = Ids(await ReadAsync(await _client.PostAsJsonAsync(SourceUrl, new QueryApiRequest<object>(null, forwardedBody), KaleidoJsonOptions.Options)));

        Assert.Equal([6, 2], direct);
        Assert.Equal(direct, forwarded);
    }

    private async Task<int[]> QueryIdsAsync(string json) => Ids(await QueryAsync(json));

    private async Task<JsonElement> QueryAsync(string json, string url = SourceUrl) => await ReadAsync(await PostAsync(url, json));

    private async Task<HttpResponseMessage> PostAsync(string url, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync(url, content);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static int[] Ids(JsonElement root) =>
        root.GetProperty("results").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToArray();
}
