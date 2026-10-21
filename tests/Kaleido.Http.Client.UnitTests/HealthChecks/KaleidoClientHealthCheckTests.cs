using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Client.HealthChecks.UnitTests;

public sealed class KaleidoClientHealthCheckTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoClientHealthCheck CreateSut(
        IHttpClientFactory httpClientFactory,
        TimeSpan? timeout = null,
        ILogger<KaleidoClientHealthCheck>? logger = null) =>
        new(
            httpClientFactory,
            "test-client",
            "/test-client/registry",
            timeout ?? TimeSpan.FromSeconds(5),
            logger ?? NullLogger<KaleidoClientHealthCheck>.Instance);

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task CheckHealthAsync_NonSuccessResponse_LogsAWarningWithTheStatusCode(HttpStatusCode statusCode)
    {
        var logger = new RecordingLogger();
        var check = CreateSut(
            FactoryFor((_, _) => Task.FromResult(new HttpResponseMessage(statusCode))),
            logger: logger);

        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal($"Kaleido client health check for 'test-client' received non-success status {(int)statusCode}.", entry.Message);
        Assert.Null(entry.Exception);
        Assert.Equal($"Kaleido remote 'test-client' returned {(int)statusCode}.", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_TransportException_LogsAWarningWithTheException()
    {
        var failure = new HttpRequestException("Connection refused");
        var logger = new RecordingLogger();
        var check = CreateSut(
            FactoryFor((_, _) => Task.FromException<HttpResponseMessage>(failure)),
            logger: logger);

        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Kaleido client health check for 'test-client' failed.", entry.Message);
        Assert.Same(failure, entry.Exception);
        Assert.Equal("Kaleido remote 'test-client' is unreachable: Connection refused", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenTheRemoteDoesNotAnswerInTime_LogsAWarning()
    {
        var logger = new RecordingLogger();
        var check = CreateSut(
            FactoryFor(async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
            TimeSpan.FromMilliseconds(50),
            logger);

        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("Kaleido client health check for 'test-client' failed.", entry.Message);
        Assert.IsAssignableFrom<OperationCanceledException>(entry.Exception);
        Assert.Same(entry.Exception, result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_SuccessResponse_LogsNothing()
    {
        var logger = new RecordingLogger();
        var check = CreateSut(
            FactoryFor((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))),
            logger: logger);

        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Empty(logger.Entries);
        Assert.Equal("Kaleido remote 'test-client' is reachable (200).", result.Description);
    }

    private sealed class RecordingLogger : ILogger<KaleidoClientHealthCheck>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }

    // The health check must report quickly and honestly: never retried by the resilience pipeline.
    [Fact]
    public async Task CheckHealthAsync_MarksItsRequestAsNeverRetried()
    {
        HttpRequestMessage? sent = null;
        var check = CreateSut(FactoryFor((request, _) =>
        {
            sent = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));

        await check.CheckHealthAsync(CreateContext());

        Assert.NotNull(sent);
        Assert.True(sent.Options.TryGetValue(KaleidoResilienceOptions.NoRetry, out var noRetry) && noRetry);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenTheRemoteDoesNotAnswerWithinItsTimeout_ReturnsUnhealthy()
    {
        var check = CreateSut(
            FactoryFor(async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
            TimeSpan.FromMilliseconds(50));

        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("did not answer within", result.Description, StringComparison.Ordinal);
    }

    private static IHttpClientFactory FactoryFor(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(respond);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost/") });
        return factory.Object;
    }

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
