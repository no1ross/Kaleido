using Kaleido.Http.Client.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Client.HealthChecks.UnitTests;

public sealed class KaleidoClientHealthCheckTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoClientHealthCheck CreateSut(
        IHttpClientFactory httpClientFactory) =>
        new(
            httpClientFactory,
            "test-client",
            "/test-client/registry",
            NullLogger<KaleidoClientHealthCheck>.Instance);

    // -------------------------------------------------------------------------
    // Healthy — remote returns 2xx
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CheckHealthAsync_SuccessResponse_ReturnsHealthy()
    {
        var check = CreateCheck(HttpStatusCode.OK);

        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.Accepted)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task CheckHealthAsync_AnySuccessStatusCode_ReturnsHealthy(HttpStatusCode statusCode)
    {
        var check = CreateCheck(statusCode);

        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    // -------------------------------------------------------------------------
    // Unhealthy — remote returns non-2xx
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task CheckHealthAsync_NonSuccessResponse_ReturnsUnhealthy(HttpStatusCode statusCode)
    {
        var check = CreateCheck(statusCode);

        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    // -------------------------------------------------------------------------
    // Unhealthy — transport exception
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CheckHealthAsync_TransportException_ReturnsUnhealthy()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Throws(new HttpRequestException("Connection refused"));

        var check = CreateSut(factory.Object);

        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    // -------------------------------------------------------------------------
    // Cancellation propagates
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CheckHealthAsync_Cancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var check = CreateSut(factory.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => check.CheckHealthAsync(CreateContext(), cts.Token));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static KaleidoClientHealthCheck CreateCheck(HttpStatusCode statusCode)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode));

        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        return CreateSut(factory.Object);
    }

    private static HealthCheckContext CreateContext() =>
        new()
        {
            Registration = new HealthCheckRegistration(
                "kaleido-test-client",
                _ => Mock.Of<IHealthCheck>(),
                HealthStatus.Unhealthy,
                [])
        };
}
