using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;
using Kaleido.Queryable.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Execution;

public sealed class NamedQueryExecutionTests
    : IClassFixture<QueryableAspNetCoreFixture>
{
    private readonly HttpClient _client;

    public NamedQueryExecutionTests(
        QueryableAspNetCoreFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task ActiveRecords_Should_Return_Only_Active_Records()
    {
        var response =
            await PostNamedQueryAsync(
                "active-records");

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
                Assert.True(
                    GetBoolean(
                        record,
                        nameof(SampleKaleidoRecord.IsActive)));
            });
    }

    [Fact]
    public async Task RecordsByCategory_Should_Filter_By_Category()
    {
        const string category =
            "Gamma";

        var response =
            await PostNamedQueryAsync(
                "records-by-category",
                new NamedQueryApiRequest(
                    new Dictionary<string, object?>
                    {
                        ["Category"] = category
                    }));

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
                    category,
                    GetString(
                        record,
                        nameof(SampleKaleidoRecord.Category)));
            });
    }

    [Fact]
    public async Task RecordsByCategory_Should_Return_BadRequest_When_Category_Missing()
    {
        var response =
            await PostNamedQueryAsync(
                "records-by-category");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task HighAmountRecords_Should_Filter_By_MinimumAmount()
    {
        const decimal minimumAmount =
            0.50m;

        var response =
            await PostNamedQueryAsync(
                "high-amount-records",
                new NamedQueryApiRequest(
                    new Dictionary<string, object?>
                    {
                        ["MinimumAmount"] = minimumAmount
                    }));

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
                Assert.True(
                    GetDecimal(
                        record,
                        nameof(SampleKaleidoRecord.Amount))
                    >= minimumAmount);
            });
    }

    [Fact]
    public async Task HighAmountRecords_Should_Use_Default_Value_When_Not_Provided()
    {
        var response =
            await PostNamedQueryAsync(
                "high-amount-records");

        await AssertStatusCodeAsync(
            HttpStatusCode.OK,
            response);

        var root =
            await ReadResponseRootAsync(response);

        Assert.True(
            GetTotalCount(root) >= 0);
    }

    [Fact]
    public async Task EffectiveOn_Should_Filter_By_EffectiveDate()
    {
        var effectiveDate =
            new DateOnly(
                2024,
                6,
                1);

        var response =
            await PostNamedQueryAsync(
                "effective-on",
                new NamedQueryApiRequest(
                    new Dictionary<string, object?>
                    {
                        ["EffectiveDate"] =
                            effectiveDate.ToString("yyyy-MM-dd")
                    }));

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
                var recordDate =
                    DateOnly.Parse(
                        GetString(
                            record,
                            nameof(SampleKaleidoRecord.EffectiveDate)));

                Assert.True(
                    recordDate <= effectiveDate);
            });
    }

    [Fact]
    public async Task EffectiveOn_Should_Return_BadRequest_When_Date_Missing()
    {
        var response =
            await PostNamedQueryAsync(
                "effective-on");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task UnknownNamedQuery_Should_Return_NotFound()
    {
        var response =
            await PostNamedQueryAsync(
                "does-not-exist");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    private Task<HttpResponseMessage> PostNamedQueryAsync(
        string queryName)
    {
        return PostNamedQueryAsync(
            queryName,
            new NamedQueryApiRequest());
    }

    private Task<HttpResponseMessage> PostNamedQueryAsync(
        string queryName,
        NamedQueryApiRequest request)
    {
        return _client.PostAsJsonAsync(
            $"/kaleido/queryable/functional-records/queries/{queryName}",
            request);
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

}