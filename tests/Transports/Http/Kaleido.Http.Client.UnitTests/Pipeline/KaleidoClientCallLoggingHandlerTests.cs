using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.Pipeline.UnitTests;

public sealed class KaleidoClientCallLoggingHandlerTests : Kaleido.UnitTests.SutFixture
{
    private readonly RecordingLogger logger = new();
    private KaleidoClientCallLoggingHandler CreateSut(HttpStatusCode status) => new(logger) { InnerHandler = new ReplyHandler(status) };
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(HttpStatusCode.OK, false)]
    [InlineData(HttpStatusCode.NotModified, false)]
    [InlineData(HttpStatusCode.BadRequest, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    public async Task SendAsync_LogsTheServiceAndSafePath_AndWarnsOnlyOnNonSuccess(HttpStatusCode status, bool warn)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gateway.test/private/step?key=do-not-log");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer do-not-log");
        request.Options.Set(KaleidoClientCallLoggingHandler.TargetService, "catalog\r\ninjected");
        using var invoker = new HttpMessageInvoker(CreateSut(status));

        using var response = await invoker.SendAsync(request, Cancel);

        Assert.Equal(status, response.StatusCode);
        var sent = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Debug);
        Assert.Contains("cataloginjected", sent.Message, StringComparison.Ordinal);
        Assert.Contains("/private/step", sent.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-log", sent.Message, StringComparison.Ordinal);
        Assert.Equal(warn ? 1 : 0, logger.Entries.Count(entry => entry.Level == LogLevel.Warning));
        Assert.All(logger.Entries, entry =>
        {
            Assert.DoesNotContain("do-not-log", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("\r", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("\n", entry.Message, StringComparison.Ordinal);
        });
        if (warn)
        {
            var failed = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
            Assert.Contains(((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture), failed.Message, StringComparison.Ordinal);
            Assert.Contains("cataloginjected", failed.Message, StringComparison.Ordinal);
        }
    }

    private sealed class ReplyHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status));
    }

    private sealed class RecordingLogger : ILogger<KaleidoClientCallLoggingHandler>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((level, formatter(state, exception)));
    }
}
