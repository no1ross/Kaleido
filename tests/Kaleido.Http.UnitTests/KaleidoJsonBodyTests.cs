using System.Text;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Http;

namespace Kaleido.Http.UnitTests;

public sealed class KaleidoJsonBodyTests
    : SutFixture
{
    private static async Task<KaleidoJsonBody<TestRequest>> CreateSut(string body, string contentType = "application/json")
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        return await KaleidoJsonBody<TestRequest>.BindAsync(context) ?? throw new InvalidOperationException("No body was bound.");
    }

    [Fact]
    public async Task Value_IsTheTypedRequest()
    {
        var body = await CreateSut("""{ "id": "6f1c2a9e-5d3b-4f7a-9c1e-2b8d4e6a0f13", "on": "2026-10-15" }""");

        Assert.Equal(Guid.Parse("6f1c2a9e-5d3b-4f7a-9c1e-2b8d4e6a0f13"), body.Value.Id);
        Assert.Equal(new DateOnly(2026, 10, 15), body.Value.On);
    }

    [Fact]
    public async Task AValueThatDoesNotFit_IsA400NamingItsPath()
    {
        var body = await CreateSut("""{ "id": "6f1c2a9e-5d3b-4f7a-9c1e-2b8d4e6a0f13", "on": "tomorrow" }""");

        var exception = Assert.Throws<BadHttpRequestException>(() => body.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Contains("'$.on'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARequiredMemberLeftOut_IsA400NamingIt()
    {
        var body = await CreateSut("""{ "on": "2026-10-15" }""");

        var exception = Assert.Throws<BadHttpRequestException>(() => body.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Contains("id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABodyThatIsNotJson_IsA400()
    {
        var body = await CreateSut("{ not json");

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.Throws<BadHttpRequestException>(() => body.Value).StatusCode);
        Assert.Equal(string.Empty, body.RawJson);
    }

    [Fact]
    public async Task ANonJsonContentType_IsA415()
    {
        var body = await CreateSut("{}", "text/plain");

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, Assert.Throws<BadHttpRequestException>(() => body.Value).StatusCode);
    }

    [Fact]
    public async Task AJsonNullBody_IsA400ForAnEmptyBody()
    {
        var body = await CreateSut("null");

        var exception = Assert.Throws<BadHttpRequestException>(() => body.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("The request body is empty.", exception.Message);
        Assert.Equal("null", body.RawJson);
    }

    [Fact]
    public async Task AnEmptyBody_IsA400WithoutAPath()
    {
        var body = await CreateSut(string.Empty);

        var exception = Assert.Throws<BadHttpRequestException>(() => body.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.StartsWith("The request body is not valid JSON: ", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("LineNumber", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RawJson_IsTheBodyAsSent()
    {
        var body = await CreateSut("""{"id":"6f1c2a9e-5d3b-4f7a-9c1e-2b8d4e6a0f13","on":"2026-10-15"}""");

        Assert.Equal("""{"id":"6f1c2a9e-5d3b-4f7a-9c1e-2b8d4e6a0f13","on":"2026-10-15"}""", body.RawJson);
    }

    public sealed class TestRequest
    {
        public required Guid Id { get; init; }

        public DateOnly On { get; init; }
    }
}
