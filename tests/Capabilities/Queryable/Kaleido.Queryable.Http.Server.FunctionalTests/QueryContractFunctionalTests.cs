using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kaleido.Eventing;
using Kaleido.Http;
using Kaleido.Http.Server;
using Kaleido.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.Http.Server.FunctionalTests.Contract;

public sealed class QueryContractFunctionalTests(ContractHost fixture) : IClassFixture<ContractHost>
{
    private const string SourceUrl = "/catalog/1/queryable/contractsource/query";
    private const string AsyncSourceUrl = "/catalog/1/queryable/asynccontractsource/query";
    private const string AsyncViewUrl = "/catalog/1/queryable/asynccontractsource/codelistview/query";
    private const string GridViewUrl = "/catalog/1/queryable/contractsource/gridview/query";

    public static TheoryData<string, string, int[]> Conditions() => new()
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
    public async Task AFilterCondition_SelectsTheMatchingRecords(string field, string operatorAndValues, int[] expected)
    {
        var ids = await QueryIdsAsync($$"""{ "query": { "filter": { "condition": { "field":"{{field}}", {{operatorAndValues}} } }, "page": { "size": 10 } } }""");
        Assert.Equal(expected, ids);
    }

    [Fact]
    public async Task FieldAndOperatorNames_AreCaseInsensitiveThroughValidationAndExecution()
    {
        var ids = await QueryIdsAsync("""{ "query": { "filter": { "condition": { "field":"category", "operator":"EQUALS", "values":["Alpha"] } } } }""");
        Assert.Equal([1, 4], ids);
    }

    [Fact]
    public async Task AnOrGroup_ReturnsTheUnionOfItsConditions()
    {
        var ids = await QueryIdsAsync("""
            { "query": { "page": { "size": 10 }, "filter": { "group": { "operator":"or", "filters": [
                { "condition": { "field":"Category", "operator":"equals", "values":["Alpha"] } },
                { "condition": { "field":"Category", "operator":"equals", "values":["Beta"] } } ] } } } }
            """);
        Assert.Equal([1, 2, 4], ids);
    }

    [Fact]
    public async Task AnAndGroup_ReturnsTheIntersectionOfItsConditions()
    {
        var ids = await QueryIdsAsync("""
            { "query": { "page": { "size": 10 }, "filter": { "group": { "operator":"and", "filters": [
                { "condition": { "field":"Category", "operator":"equals", "values":["Gamma"] } },
                { "condition": { "field":"Amount", "operator":"greaterthanorequal", "values":[50] } } ] } } } }
            """);
        Assert.Equal([6], ids);
    }

    [Fact]
    public async Task NestedGroups_CombineAndOrBeforePageAndSort()
    {
        var ids = await QueryIdsAsync("""
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
        var ids = await QueryIdsAsync("""{ "query": { "page": { "size": 10 }, "sort": [ { "field":"Category", "direction":"ascending" }, { "field":"Amount", "direction":"descending" } ] } }""");
        Assert.Equal([4, 1, 2, 5, 6, 3], ids);
    }

    [Fact]
    public async Task ExplicitSortSequence_WinsOverListPosition()
    {
        var ids = await QueryIdsAsync("""{ "query": { "page": { "size": 10 }, "sort": [ { "field":"Amount", "direction":"descending", "sequence": 2 }, { "field":"Category", "direction":"ascending", "sequence": 1 } ] } }""");
        Assert.Equal([4, 1, 2, 5, 6, 3], ids);
    }

    [Fact]
    public async Task SearchText_MatchesAcrossSearchableFields()
    {
        var west = await QueryIdsAsync("""{ "query": { "searchText": "est" } }""");
        var gamma = await QueryIdsAsync("""{ "query": { "searchText": "amma" } }""");
        Assert.Equal([2], west);
        Assert.Equal([3, 6], gamma);
    }

    [Fact]
    public async Task SearchFilterSortAndPage_ComposeInOneRequest()
    {
        var root = await QueryAsync("""
            { "query": { "searchText": "a", "page": { "size": 2, "offset": 1 },
              "filter": { "condition": { "field":"IsActive", "operator":"istrue" } },
              "sort": [ { "field":"Amount", "direction":"descending" } ] } }
            """);
        Assert.Equal(4, root.GetProperty("totalCount").GetInt32());
        Assert.Equal([4, 3], Ids(root));
    }

    [Fact]
    public async Task EnumValues_AreCamelCaseAndCanBeFilteredBack()
    {
        var root = await QueryAsync("""{ "query": { "filter": { "condition": { "field":"Id", "operator":"equals", "values":[4] } } } }""");
        var record = Assert.Single(root.GetProperty("results").EnumerateArray());
        Assert.Equal(JsonValueKind.String, record.GetProperty("status").ValueKind);
        Assert.Equal("suspended", record.GetProperty("status").GetString());
        var echoed = await QueryIdsAsync("""{ "query": { "filter": { "condition": { "field":"Status", "operator":"equals", "values":["suspended"] } } } }""");
        Assert.Equal([4], echoed);
    }

    public static TheoryData<string, string> InvalidRequests() => new()
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
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequests_ReturnStructured400(string json, string code)
    {
        using var response = await PostRawAsync(json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("EffectiveDate", "\"not-a-date\"")]
    [InlineData("Amount", "\"lots\"")]
    [InlineData("ExternalId", "\"not-a-guid\"")]
    [InlineData("Status", "\"Unheard\"")]
    public async Task InvalidTypedFilterValue_Is400NotAServerError(string field, string value)
    {
        using var response = await PostRawAsync($$"""{ "query": { "filter": { "condition": { "field":"{{field}}", "operator":"equals", "values":[{{value}}] } } } }""");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnAsyncSource_IsAwaitedAndFilteredLikeADirectSource()
    {
        var root = await QueryAsync("""{ "query": { "filter": { "condition": { "field":"Category", "operator":"equals", "values":["Gamma"] } } } }""", AsyncSourceUrl);
        Assert.Equal(2, root.GetProperty("totalCount").GetInt32());
        Assert.Equal([3, 6], Ids(root));
    }

    [Fact]
    public async Task AnAsyncView_OwnsItsDefaultPageAndOrder()
    {
        var root = await QueryAsync("""{ "query": {} }""", AsyncViewUrl);
        Assert.Equal(6, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(["AL-001", "AL-004"], root.GetProperty("results").EnumerateArray().Select(item => item.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task AView_EnforcesItsOwnMaximumPageSize()
    {
        using var response = await PostRawAsync("""{ "query": { "page": { "size": 5 } } }""", AsyncViewUrl);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("qry_invalid_page_size", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AView_FiltersTheContextBeforeProjecting()
    {
        var root = await QueryAsync("""{ "query": { "filter": { "condition": { "field":"Amount", "operator":"greaterthanorequal", "values":[55] } } } }""", AsyncViewUrl);
        Assert.Equal(3, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(["AL-004", "DE-005"], root.GetProperty("results").EnumerateArray().Select(item => item.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task AView_WithoutItsRequiredParameter_Is400()
    {
        using var response = await PostRawAsync("""{ "query": {} }""", GridViewUrl);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("qry_missing_parameter", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AForwardedNeutralQuery_HasTheSameWireResultsAsItsOriginalRequest()
    {
        var original = """
            { "query": { "page": { "size": 10 },
              "filter": { "group": { "operator":"or", "filters": [
                { "group": { "operator":"and", "filters": [
                  { "condition": { "field":"Status", "operator":"equals", "values":["active"] } },
                  { "condition": { "field":"Amount", "operator":"greaterthanorequal", "values":[40.5] } } ] } },
                { "condition": { "field":"EffectiveDate", "operator":"equals", "values":["2024-01-05"] } } ] } },
              "sort": [ { "field":"Amount", "direction":"descending" } ] } }
            """;
        var forwarded = new QueryBody(
            Filter: new QueryFilterNode(null, new QueryFilterGroup(LogicalOperator.Or,
            [
                new QueryFilterNode(null, new QueryFilterGroup(LogicalOperator.And,
                [
                    QueryFilterNode.CreateCondition("Status", FilterOperator.Equals, RecordStatus.Active),
                    QueryFilterNode.CreateCondition("Amount", FilterOperator.GreaterThanOrEqual, 40.5m),
                ])),
                QueryFilterNode.CreateCondition("EffectiveDate", FilterOperator.Equals, new DateOnly(2024, 1, 5)),
            ])),
            Sort: [new QuerySort("Amount", SortDirection.Descending)],
            Page: new QueryPage(10, 0)).ToApiBody();
        var direct = await QueryIdsAsync(original);
        using var response = await fixture.Client.PostAsJsonAsync(SourceUrl, new QueryApiRequest<object>(null, forwarded), KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal([6, 2], direct);
        Assert.Equal(direct, Ids(document.RootElement));
    }

    private async Task<int[]> QueryIdsAsync(string json) => Ids(await QueryAsync(json));

    private async Task<HttpResponseMessage> PostRawAsync(string json, string url = SourceUrl)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await fixture.Client.PostAsync(url, content, TestContext.Current.CancellationToken);
    }

    private async Task<JsonElement> QueryAsync(string json, string url = SourceUrl)
    {
        using var response = await PostRawAsync(json, url);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static int[] Ids(JsonElement root) => root.GetProperty("results").EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToArray();
}

public sealed class ContractHost : IAsyncLifetime
{
    private WebApplication? app;
    public HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var outbox = new Mock<IKaleidoOutboxStore>();
        outbox.Setup(store => store.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(outbox.Object);
        builder.Services.AddSingleton<ContractRecords>();
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "catalog",
            ServiceVersion = "1",
            Assemblies = [typeof(ContractHost).Assembly],
            TypeFilter = type => type.Namespace == typeof(ContractSource).Namespace,
        })
            .AddEventing()
            .AddQueryable()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp();
        app = builder.Build();
        app.MapQueryable();
        await app.StartAsync(TestContext.Current.CancellationToken);
        Client = app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        if (app is not null)
        {
            await app.StopAsync(CancellationToken.None);
            await app.DisposeAsync();
        }
    }
}
