using Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
using Kaleido.Queryable.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Execution;

public sealed class QueryExecutionTests
    : IClassFixture<QueryableAspNetCoreFixture>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter(
                    new LowerInvariantJsonNamingPolicy(),
                    allowIntegerValues: false)
            }
        };

    private readonly HttpClient _client;

    public QueryExecutionTests(
        QueryableAspNetCoreFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task PostQuery_Should_Return_Ok_For_Empty_Query()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody());

        var response =
            await PostQueryAsync(request);

        await AssertStatusCodeAsync(
            HttpStatusCode.OK,
            response);
    }

    [Fact]
    public async Task PostQuery_Should_Return_Records_For_Empty_Query()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody());

        var response =
            await PostQueryAsync(request);

        await AssertStatusCodeAsync(
            HttpStatusCode.OK,
            response);

        var root =
            await ReadResponseRootAsync(response);

        var records =
            GetRecords(root);

        Assert.NotEmpty(records);

        Assert.True(
            GetTotalCount(root) >= records.Length);

        Assert.Equal(
            0,
            GetOffset(root));

        Assert.Equal(
            records.Length,
            GetPageSize(root));
    }

    [Fact]
    public async Task PostQuery_Should_Filter_By_String_Equality()
    {
        var seedRecords =
            await PostQueryForRecordsAsync(
                new QueryRequest(
                    Query: new QueryBody()));

        var category =
            GetString(
                seedRecords[0],
                nameof(SampleKaleidoRecord.Category));

        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter: QueryFilterNode.CreateCondition(
                        nameof(SampleKaleidoRecord.Category),
                        FilterOperator.Equals,
                        category)));

        var records =
            await PostQueryForRecordsAsync(request);

        Assert.NotEmpty(records);

        Assert.All(
            records,
            record =>
            {
                Assert.Equal(
                    category,
                    GetString(
                        record,
                        nameof(SampleKaleidoRecord.Category)));
            });
    }

    [Fact]
    public async Task PostQuery_Should_Filter_By_String_In()
    {
        var seedRecords =
            await PostQueryForRecordsAsync(
                new QueryRequest(
                    Query: new QueryBody()));

        var categories =
            seedRecords
                .Select(record =>
                    GetString(
                        record,
                        nameof(SampleKaleidoRecord.Category)))
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .ToArray();

        Assert.Equal(
            2,
            categories.Length);

        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter: QueryFilterNode.CreateCondition(
                        nameof(SampleKaleidoRecord.Category),
                        FilterOperator.In,
                        categories.Cast<object?>().ToArray())));

        var records =
            await PostQueryForRecordsAsync(request);

        Assert.NotEmpty(records);

        Assert.All(
            records,
            record =>
            {
                Assert.Contains(
                    GetString(
                        record,
                        nameof(SampleKaleidoRecord.Category)),
                    categories);
            });
    }

    [Fact]
    public async Task PostQuery_Should_Filter_By_Boolean_IsTrue()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter: QueryFilterNode.CreateCondition(
                        nameof(SampleKaleidoRecord.IsActive),
                        FilterOperator.IsTrue)));

        var records =
            await PostQueryForRecordsAsync(request);

        Assert.NotEmpty(records);

        Assert.All(
            records,
            record =>
            {
                Assert.True(
                    GetBoolean(
                        record,
                        nameof(SampleKaleidoRecord.IsActive)));
            });
    }

    [Fact]
    public async Task PostQuery_Should_Filter_By_Boolean_IsFalse()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter: QueryFilterNode.CreateCondition(
                        nameof(SampleKaleidoRecord.IsActive),
                        FilterOperator.IsFalse)));

        var records =
            await PostQueryForRecordsAsync(request);

        Assert.NotEmpty(records);

        Assert.All(
            records,
            record =>
            {
                Assert.False(
                    GetBoolean(
                        record,
                        nameof(SampleKaleidoRecord.IsActive)));
            });
    }

    [Fact]
    public async Task PostQuery_Should_Filter_By_Number_GreaterThanOrEqual()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter: QueryFilterNode.CreateCondition(
                        nameof(SampleKaleidoRecord.Amount),
                        FilterOperator.GreaterThanOrEqual,
                        40m)));

        var records =
            await PostQueryForRecordsAsync(request);

        Assert.NotEmpty(records);

        Assert.All(
            records,
            record =>
            {
                Assert.True(
                    GetDecimal(
                        record,
                        nameof(SampleKaleidoRecord.Amount)) >= 40m);
            });
    }

    [Fact]
    public async Task PostQuery_Should_Filter_By_Number_Between()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter: QueryFilterNode.CreateCondition(
                        nameof(SampleKaleidoRecord.Amount),
                        FilterOperator.Between,
                        20m,
                        40m)));

        var records =
            await PostQueryForRecordsAsync(request);

        Assert.NotEmpty(records);

        Assert.All(
            records,
            record =>
            {
                var amount =
                    GetDecimal(
                        record,
                        nameof(SampleKaleidoRecord.Amount));

                Assert.True(
                    amount >= 20m);

                Assert.True(
                    amount <= 40m);
            });
    }

    [Fact]
    public async Task PostQuery_Should_Filter_By_Enum_String_Value()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter: QueryFilterNode.CreateCondition(
                        nameof(SampleKaleidoRecord.Status),
                        FilterOperator.Equals,
                        "Active")));

        var records =
            await PostQueryForRecordsAsync(request);

        Assert.NotEmpty(records);

        Assert.All(
            records,
            record =>
            {
                Assert.Equal(
                    "Active",
                    GetString(
                        record,
                        nameof(SampleKaleidoRecord.Status)));
            });
    }

    [Fact]
    public async Task PostQuery_Should_Serialize_Enum_Values_As_Lowercase_Strings()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody());

        var records =
            await PostQueryForRecordsAsync(request);

        Assert.NotEmpty(records);

        Assert.All(
            records,
            record =>
            {
                var status =
                    GetProperty(
                        record,
                        nameof(SampleKaleidoRecord.Status));

                Assert.Equal(
                    JsonValueKind.String,
                    status.ValueKind);

                Assert.Contains(
                    status.GetString(),
                    new[]
                    {
                        "Unknown",
                        "Draft",
                        "Active",
                        "Suspended",
                        "Retired"
                    });
            });
    }

    [Fact]
    public async Task PostQuery_Should_Filter_By_Group_And()
    {
        var seedRecords =
            await PostQueryForRecordsAsync(
                new QueryRequest(
                    Query: new QueryBody()));

        var activeRecord =
            seedRecords.First(
                record =>
                    GetBoolean(
                        record,
                        nameof(SampleKaleidoRecord.IsActive)));

        var category =
            GetString(
                activeRecord,
                nameof(SampleKaleidoRecord.Category));

        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter: QueryFilterNode.CreateGroup(
                        LogicalOperator.And,
                        QueryFilterNode.CreateCondition(
                            nameof(SampleKaleidoRecord.IsActive),
                            FilterOperator.IsTrue),
                        QueryFilterNode.CreateCondition(
                            nameof(SampleKaleidoRecord.Category),
                            FilterOperator.Equals,
                            category))));

        var records =
            await PostQueryForRecordsAsync(request);

        Assert.NotEmpty(records);

        Assert.All(
            records,
            record =>
            {
                Assert.True(
                    GetBoolean(
                        record,
                        nameof(SampleKaleidoRecord.IsActive)));

                Assert.Equal(
                    category,
                    GetString(
                        record,
                        nameof(SampleKaleidoRecord.Category)));
            });
    }

    [Fact]
    public async Task PostQuery_Should_Filter_By_Group_Or()
    {
        var seedRecords =
            await PostQueryForRecordsAsync(
                new QueryRequest(
                    Query: new QueryBody()));

        var categories =
            seedRecords
                .Select(record =>
                    GetString(
                        record,
                        nameof(SampleKaleidoRecord.Category)))
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .ToArray();

        Assert.Equal(
            2,
            categories.Length);

        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter: QueryFilterNode.CreateGroup(
                        LogicalOperator.Or,
                        QueryFilterNode.CreateCondition(
                            nameof(SampleKaleidoRecord.Category),
                            FilterOperator.Equals,
                            categories[0]),
                        QueryFilterNode.CreateCondition(
                            nameof(SampleKaleidoRecord.Category),
                            FilterOperator.Equals,
                            categories[1]))));

        var records =
            await PostQueryForRecordsAsync(request);

        Assert.NotEmpty(records);

        Assert.All(
            records,
            record =>
            {
                Assert.Contains(
                    GetString(
                        record,
                        nameof(SampleKaleidoRecord.Category)),
                    categories);
            });
    }

    [Fact]
    public async Task PostQuery_Should_Search_By_Field_Contains()
    {
        var seedRecords =
            await PostQueryForRecordsAsync(
                new QueryRequest(
                    Query: new QueryBody()));

        var name =
            GetString(
                seedRecords[0],
                nameof(SampleKaleidoRecord.Name));

        var searchText =
            name.Length > 3
                ? name[..3]
                : name;

        var request =
            new QueryRequest(
                Query: new QueryBody(
                    SearchText: searchText));

        var records =
            await PostQueryForRecordsAsync(request);

        Assert.NotEmpty(records);

        Assert.All(
            records,
            record =>
            {
                Assert.Contains(
                    searchText,
                    GetString(
                        record,
                        nameof(SampleKaleidoRecord.Name)),
                    StringComparison.OrdinalIgnoreCase);
            });
    }

    [Fact]
    public async Task PostQuery_Should_Search_By_Group_Or()
    {
        var seedRecords =
            await PostQueryForRecordsAsync(
                new QueryRequest(
                    Query: new QueryBody()));

        Assert.True(
            seedRecords.Length >= 2);

        var firstCode =
            GetString(
                seedRecords[0],
                nameof(SampleKaleidoRecord.Code));

        var secondCode =
            GetString(
                seedRecords[1],
                nameof(SampleKaleidoRecord.Code));

        var expectedCodes =
            new[]
            {
                firstCode,
                secondCode
            };

        var request =
            new QueryRequest(
                Query: new QueryBody(
                    SearchText : ""));

        var records =
            await PostQueryForRecordsAsync(request);

        Assert.NotEmpty(records);

        Assert.All(
            records,
            record =>
            {
                Assert.Contains(
                    GetString(
                        record,
                        nameof(SampleKaleidoRecord.Code)),
                    expectedCodes);
            });
    }

    [Fact]
    public async Task PostQuery_Should_Sort_By_Amount_Ascending()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Sort:
                    [
                        new QuerySort(
                            nameof(SampleKaleidoRecord.Amount),
                            SortDirection.Ascending)
                    ]));

        var records =
            await PostQueryForRecordsAsync(request);

        Assert.NotEmpty(records);

        var amounts =
            records
                .Select(record =>
                    GetDecimal(
                        record,
                        nameof(SampleKaleidoRecord.Amount)))
                .ToArray();

        Assert.Equal(
            amounts.OrderBy(x => x).ToArray(),
            amounts);
    }

    [Fact]
    public async Task PostQuery_Should_Sort_By_Category_Then_Amount()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Sort:
                    [
                        new QuerySort(
                            nameof(SampleKaleidoRecord.Category),
                            SortDirection.Ascending,
                            1),
                        new QuerySort(
                            nameof(SampleKaleidoRecord.Amount),
                            SortDirection.Descending,
                            2)
                    ]));

        var records =
            await PostQueryForRecordsAsync(request);

        Assert.NotEmpty(records);

        var actual =
            records
                .Select(record => new SortProjection(
                    GetString(
                        record,
                        nameof(SampleKaleidoRecord.Category)),
                    GetDecimal(
                        record,
                        nameof(SampleKaleidoRecord.Amount))))
                .ToArray();

        var expected =
            actual
                .OrderBy(x => x.Category)
                .ThenByDescending(x => x.Amount)
                .ToArray();

        Assert.Equal(
            expected,
            actual);
    }

    [Fact]
    public async Task PostQuery_Should_Page_Results()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Sort:
                    [
                        new QuerySort(
                            nameof(SampleKaleidoRecord.Id),
                            SortDirection.Ascending)
                    ],
                    Page: new QueryPage(
                        2,
                        0)));

        var response =
            await PostQueryAsync(request);

        await AssertStatusCodeAsync(
            HttpStatusCode.OK,
            response);

        var root =
            await ReadResponseRootAsync(response);

        var records =
            GetRecords(root);

        Assert.Equal(
            2,
            records.Length);

        Assert.Equal(
            0,
            GetOffset(root));

        Assert.Equal(
            2,
            GetPageSize(root));

        Assert.True(
            GetTotalCount(root) >= records.Length);
    }

    [Fact]
    public async Task PostQuery_Should_Apply_Page_Offset()
    {
        var firstPageRequest =
            new QueryRequest(
                Query: new QueryBody(
                    Sort:
                    [
                        new QuerySort(
                            nameof(SampleKaleidoRecord.Id),
                            SortDirection.Ascending)
                    ],
                    Page: new QueryPage(
                        2,
                        0)));

        var secondPageRequest =
            new QueryRequest(
                Query: new QueryBody(
                    Sort:
                    [
                        new QuerySort(
                            nameof(SampleKaleidoRecord.Id),
                            SortDirection.Ascending)
                    ],
                    Page: new QueryPage(
                        2,
                        2)));

        var firstPageResponse =
            await PostQueryAsync(firstPageRequest);

        await AssertStatusCodeAsync(
            HttpStatusCode.OK,
            firstPageResponse);

        var secondPageResponse =
            await PostQueryAsync(secondPageRequest);

        await AssertStatusCodeAsync(
            HttpStatusCode.OK,
            secondPageResponse);

        var firstPageRoot =
            await ReadResponseRootAsync(firstPageResponse);

        var secondPageRoot =
            await ReadResponseRootAsync(secondPageResponse);

        var firstPageRecords =
            GetRecords(firstPageRoot);

        var secondPageRecords =
            GetRecords(secondPageRoot);

        Assert.Equal(
            2,
            firstPageRecords.Length);

        Assert.Equal(
            2,
            secondPageRecords.Length);

        Assert.Equal(
            0,
            GetOffset(firstPageRoot));

        Assert.Equal(
            2,
            GetOffset(secondPageRoot));

        Assert.Equal(
            2,
            GetPageSize(firstPageRoot));

        Assert.Equal(
            2,
            GetPageSize(secondPageRoot));

        Assert.NotEqual(
            GetInt32(
                firstPageRecords[0],
                nameof(SampleKaleidoRecord.Id)),
            GetInt32(
                secondPageRecords[0],
                nameof(SampleKaleidoRecord.Id)));
    }

    [Fact]
    public async Task PostQuery_Should_Apply_Filter_Search_Sort_And_Page()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    SearchText: "a",
                    Filter: QueryFilterNode.CreateCondition(
                        nameof(SampleKaleidoRecord.IsActive),
                        FilterOperator.IsTrue),
                    Sort:
                    [
                        new QuerySort(
                            nameof(SampleKaleidoRecord.Amount),
                            SortDirection.Descending)
                    ],
                    Page: new QueryPage(
                        5,
                        0)));

        var response =
            await PostQueryAsync(request);

        await AssertStatusCodeAsync(
            HttpStatusCode.OK,
            response);

        var root =
            await ReadResponseRootAsync(response);

        var records =
            GetRecords(root);

        Assert.True(
            records.Length <= 5);

        Assert.Equal(
            0,
            GetOffset(root));

        Assert.Equal(
            5,
            GetPageSize(root));

        Assert.All(
            records,
            record =>
            {
                Assert.True(
                    GetBoolean(
                        record,
                        nameof(SampleKaleidoRecord.IsActive)));

                Assert.Contains(
                    "a",
                    GetString(
                        record,
                        nameof(SampleKaleidoRecord.Name)),
                    StringComparison.OrdinalIgnoreCase);
            });

        var amounts =
            records
                .Select(record =>
                    GetDecimal(
                        record,
                        nameof(SampleKaleidoRecord.Amount)))
                .ToArray();

        Assert.Equal(
            amounts.OrderByDescending(x => x).ToArray(),
            amounts);
    }

    [Fact]
    public async Task PostQuery_Should_Accept_Raw_Json_With_String_Enum_Values()
    {
        var json =
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
            """;

        var response =
            await PostRawQueryAsync(json);

        await AssertStatusCodeAsync(
            HttpStatusCode.OK,
            response);

        var records =
            await ReadRecordsAsync(response);

        Assert.NotEmpty(records);

        Assert.All(
            records,
            record =>
            {
                Assert.Equal(
                    "Active",
                    GetString(
                        record,
                        nameof(SampleKaleidoRecord.Status)));
            });
    }

    [Fact]
    public async Task PostQuery_Should_Return_NotFound_For_Unknown_Record()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody());

        var response =
            await PostQueryAsync(
                "/kaleido/queryable/unknown-records/query",
                request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task PostQuery_Should_Return_BadRequest_For_Unknown_Field()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter: QueryFilterNode.CreateCondition(
                        "DoesNotExist",
                        FilterOperator.Equals,
                        "anything")));

        var response =
            await PostQueryAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task PostQuery_Should_Return_BadRequest_For_Invalid_Filter_Node()
    {
        var json =
            """
            {
              "query": {
                "filter": {
                  "condition": null,
                  "group": null
                }
              }
            }
            """;

        var response =
            await PostRawQueryAsync(json);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    private async Task<JsonElement[]> PostQueryForRecordsAsync(
        QueryRequest request)
    {
        var response =
            await PostQueryAsync(request);

        await AssertStatusCodeAsync(
            HttpStatusCode.OK,
            response);

        return await ReadRecordsAsync(response);
    }

    private Task<HttpResponseMessage> PostQueryAsync(
        QueryRequest request)
    {
        return PostQueryAsync(
            "/kaleido/queryable/functional-records/query",
            request);
    }

    private Task<HttpResponseMessage> PostQueryAsync(
        string url,
        QueryRequest request)
    {
        return _client.PostAsJsonAsync(
            url,
            request,
            JsonOptions);
    }

    private async Task<HttpResponseMessage> PostRawQueryAsync(
        string json)
    {
        using var content =
            new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

        return await _client.PostAsync(
            "/kaleido/queryable/functional-records/query",
            content);
    }

    private static async Task AssertStatusCodeAsync(
        HttpStatusCode expected,
        HttpResponseMessage response)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        var content =
            await response.Content.ReadAsStringAsync();

        Assert.Fail(
            $"Expected status code {expected}, but received {response.StatusCode}. Response body: {content}");
    }

    private static async Task<JsonElement> ReadResponseRootAsync(
        HttpResponseMessage response)
    {
        var json =
            await response.Content.ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }

    private static async Task<JsonElement[]> ReadRecordsAsync(
        HttpResponseMessage response)
    {
        var root =
            await ReadResponseRootAsync(response);

        return GetRecords(root);
    }

    private static JsonElement[] GetRecords(
        JsonElement root)
    {
        if (!root.TryGetProperty(
                "records",
                out var records))
        {
            throw new InvalidOperationException(
                "The query response did not contain a 'records' property.");
        }

        if (records.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "The query response 'records' property was not an array.");
        }

        return records
            .EnumerateArray()
            .Select(record => record.Clone())
            .ToArray();
    }

    private static int GetTotalCount(
        JsonElement root)
    {
        return GetInt32(
            root,
            "totalCount");
    }

    private static int GetOffset(
        JsonElement root)
    {
        return GetInt32(
            root,
            "offset");
    }

    private static int GetPageSize(
        JsonElement root)
    {
        return GetInt32(
            root,
            "pageSize");
    }

    private static string GetString(
        JsonElement element,
        string propertyName)
    {
        var value =
            GetProperty(
                element,
                propertyName);

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()!;
        }

        return value.ToString();
    }

    private static bool GetBoolean(
        JsonElement element,
        string propertyName)
    {
        var value =
            GetProperty(
                element,
                propertyName);

        return value.GetBoolean();
    }

    private static int GetInt32(
        JsonElement element,
        string propertyName)
    {
        var value =
            GetProperty(
                element,
                propertyName);

        return value.GetInt32();
    }

    private static decimal GetDecimal(
        JsonElement element,
        string propertyName)
    {
        var value =
            GetProperty(
                element,
                propertyName);

        return value.GetDecimal();
    }

    private static JsonElement GetProperty(
        JsonElement element,
        string propertyName)
    {
        if (element.TryGetProperty(
                propertyName,
                out var value))
        {
            return value;
        }

        var camelName =
            ToCamelCase(propertyName);

        if (element.TryGetProperty(
                camelName,
                out value))
        {
            return value;
        }

        throw new InvalidOperationException(
            $"Property '{propertyName}' was not found.");
    }

    private static string ToCamelCase(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) +
               value[1..];
    }

    private sealed record SortProjection(
        string Category,
        decimal Amount);

    private sealed class LowerInvariantJsonNamingPolicy
        : JsonNamingPolicy
    {
        public override string ConvertName(
            string name)
        {
            return name.ToLowerInvariant();
        }
    }
}