using System.Text.Json;

namespace Kaleido.UnitTests;

public sealed class KaleidoErrorResponseTests
    : SutFixture<KaleidoErrorResponse>
{
    private static readonly JsonSerializerOptions WebOptions =
        new(JsonSerializerDefaults.Web);

    protected override KaleidoErrorResponse CreateSut() =>
        CreateSut([]);

    private static KaleidoErrorResponse CreateSut(
        params KaleidoError[] errors) =>
        new(errors);

    [Fact]
    public void Serialize_UsesCamelCaseContractShape()
    {
        var response = CreateSut(
            [new KaleidoError("qry_bad", "Something failed", "fieldX")]);

        var json = JsonSerializer.Serialize(response, WebOptions);

        Assert.Equal(
            "{\"errors\":[{\"code\":\"qry_bad\",\"message\":\"Something failed\",\"field\":\"fieldX\"}]}",
            json);
    }

    [Fact]
    public void Serialize_WhenFieldNull_IncludesFieldNull()
    {
        var response = CreateSut(
            [new KaleidoError("framework_error", "An unexpected error occurred.")]);

        var json = JsonSerializer.Serialize(response, WebOptions);

        Assert.Contains("\"field\":null", json);
    }

    [Fact]
    public void Serialize_WhenMultipleErrors_PreservesOrder()
    {
        var response = CreateSut(
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
