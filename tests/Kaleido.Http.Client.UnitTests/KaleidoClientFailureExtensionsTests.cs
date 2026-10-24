using System.Text;
using Kaleido.UnitTests;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoClientFailureExtensionsTests
    : SutFixture
{
    private static HttpResponseMessage WithErrors(HttpStatusCode status, params KaleidoError[] errors) =>
        new(status) { Content = JsonContent.Create(new KaleidoErrorResponse(errors), options: KaleidoJsonOptions.Options) };

    private static HttpResponseMessage WithBody(HttpStatusCode status, string body, string mediaType) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

    // #444: the service's codes survive every status, not only 400/409.
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, HttpClientErrorCodes.ValidationFailed)]
    [InlineData(HttpStatusCode.Conflict, HttpClientErrorCodes.Conflict)]
    [InlineData(HttpStatusCode.Forbidden, HttpClientErrorCodes.RequestFailed)]
    [InlineData(HttpStatusCode.NotFound, HttpClientErrorCodes.RequestFailed)]
    [InlineData(HttpStatusCode.UnprocessableEntity, HttpClientErrorCodes.RequestFailed)]
    [InlineData(HttpStatusCode.InternalServerError, HttpClientErrorCodes.RequestFailed)]
    public async Task ToClientFailureAsync_AKaleidoErrorBody_KeepsTheServicesErrors_ForAnyStatus(HttpStatusCode status, string expectedCode)
    {
        using var response = WithErrors(status, new KaleidoError("pro_idempotency_key_reused", "The key was used for another request."));

        var exception = await response.ToClientFailureAsync("Process step request failed", TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(status, exception.StatusCode);
        Assert.Equal("pro_idempotency_key_reused", Assert.Single(exception.Errors).Code);
        Assert.Equal("Process step request failed: The key was used for another request.", exception.Message);
    }

    // #444: a body that is not a Kaleido error body never escapes as a JsonException.
    [Theory]
    [InlineData("<html><body>Bad Gateway</body></html>", "text/html")]
    [InlineData("upstream unavailable", "text/plain")]
    [InlineData("", "application/json")]
    [InlineData("null", "application/json")]
    [InlineData("{\"errors\":[]}", "application/json")]
    public async Task ToClientFailureAsync_WithoutKaleidoErrors_IsRequestFailedWithTheStatus(string body, string mediaType)
    {
        using var response = WithBody(HttpStatusCode.BadRequest, body, mediaType);

        var exception = await response.ToClientFailureAsync("Query failed", TestContext.Current.CancellationToken);

        Assert.Equal(HttpClientErrorCodes.RequestFailed, exception.Code);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Empty(exception.Errors);
        Assert.Equal("Query failed with status code 400 (BadRequest).", exception.Message);
    }

    [Fact]
    public async Task ToClientFailureAsync_AnEmptyResponse_IsRequestFailed()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        var exception = await response.ToClientFailureAsync("Query failed", TestContext.Current.CancellationToken);

        Assert.Equal(HttpClientErrorCodes.RequestFailed, exception.Code);
        Assert.Equal("Query failed with status code 503 (ServiceUnavailable).", exception.Message);
    }

    [Fact]
    public async Task ToClientFailureAsync_WithoutAResponse_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ((HttpResponseMessage)null!).ToClientFailureAsync("x", TestContext.Current.CancellationToken));
    }
}
