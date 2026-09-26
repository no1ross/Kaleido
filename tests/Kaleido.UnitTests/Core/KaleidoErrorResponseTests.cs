using System.Text.Json;

namespace Kaleido.UnitTests.Core;

public sealed class KaleidoErrorResponseTests
{
    private static readonly JsonSerializerOptions WebOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void Serialize_UsesCamelCaseContractShape()
    {
        var response = new KaleidoErrorResponse(
            [new KaleidoError("qry_bad", "Something failed", "fieldX")]);

        var json = JsonSerializer.Serialize(response, WebOptions);

        Assert.Equal(
            "{\"errors\":[{\"code\":\"qry_bad\",\"message\":\"Something failed\",\"field\":\"fieldX\"}]}",
            json);
    }

    [Fact]
    public void Serialize_WhenFieldNull_IncludesFieldNull()
    {
        var response = new KaleidoErrorResponse(
            [new KaleidoError("framework_error", "An unexpected error occurred.")]);

        var json = JsonSerializer.Serialize(response, WebOptions);

        Assert.Contains("\"field\":null", json);
    }

    [Fact]
    public void Serialize_WhenMultipleErrors_PreservesOrder()
    {
        var response = new KaleidoErrorResponse(
            [
                new KaleidoError("first", "1"),
                new KaleidoError("second", "2")
            ]);

        var json = JsonSerializer.Serialize(response, WebOptions);

        Assert.True(
            json.IndexOf("\"first\"", StringComparison.Ordinal)
                < json.IndexOf("\"second\"", StringComparison.Ordinal));
    }
}
