using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.Registration.UnitTests;

public sealed class KaleidoClientTransportCheckTests : SutFixture
{
    private readonly RecordingLogger logger = new();

    private KaleidoClientTransportCheck CreateSut(string? gateway) => new(new KaleidoClientOptions { BaseUrl = gateway }, logger);

    [Fact]
    public async Task StartAsync_WarnsOnce_WhenTheGatewayUsesPlainHttp()
    {
        await CreateSut("http://gateway.internal:8080/api").StartAsync(TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Kaleido client 'kaleido-gateway' calls http://gateway.internal:8080 without HTTPS: a caller's credential would be sent in clear text. Use an https Kaleido:BaseUrl for the gateway.", entry.Message);
    }

    [Fact]
    public async Task StartAsync_AnEmbeddedCredentialIsNeverLogged()
    {
        await CreateSut("http://user:secret@gateway.internal:8080/").StartAsync(TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.DoesNotContain("user:secret", entry.Message, StringComparison.Ordinal);
        Assert.Contains("http://gateway.internal:8080", entry.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://localhost:5101/")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://[::1]/")]
    [InlineData("https://gateway.internal/")]
    [InlineData("HTTPS://gateway.internal/")]
    public async Task StartAsync_HttpsOrLoopback_DoesNotWarn(string gateway)
    {
        await CreateSut(gateway).StartAsync(TestContext.Current.CancellationToken);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task StopAsync_Completes()
    {
        await CreateSut(null).StopAsync(TestContext.Current.CancellationToken);
        Assert.Empty(logger.Entries);
    }

    private sealed class RecordingLogger : ILogger<KaleidoClientTransportCheck>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
