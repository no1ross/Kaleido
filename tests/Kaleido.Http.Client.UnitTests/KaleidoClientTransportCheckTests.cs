using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoClientTransportCheckTests
    : SutFixture
{
    private readonly RecordingLogger logger = new();

    private KaleidoClientTransportCheck CreateSut(KaleidoClientOptions options, bool withProvider = true) =>
        new(
            options,
            withProvider ? [Mock.Of<IKaleidoCallCredentialProvider>()] : [],
            logger);

    private static KaleidoClientOptions Clients(string? sharedBaseUrl, params (string Name, string? BaseUrl)[] clients) =>
        new()
        {
            BaseUrl = sharedBaseUrl,
            Clients = clients.ToDictionary(c => c.Name, c => new KaleidoClientEntry { BaseUrl = c.BaseUrl }, StringComparer.OrdinalIgnoreCase),
        };

    [Fact]
    public async Task StartAsync_WarnsOnce_ForEachClientWithANonHttpsRemoteBaseUrl()
    {
        var sut = CreateSut(Clients(
            sharedBaseUrl: "http://shared.internal/",
            ("Member", "http://member.internal:8080/api"),
            ("Radiology", "https://radiology.internal/"),
            ("Intake", null)));

        await sut.StartAsync(TestContext.Current.CancellationToken);

        Assert.All(logger.Entries, e => Assert.Equal(LogLevel.Warning, e.Level));
        Assert.Equal(
            [
                "Kaleido client 'Intake' calls http://shared.internal without HTTPS while call credentials are configured: a caller's credential would be sent in clear text. Use an https BaseUrl (Kaleido:Clients:Intake:BaseUrl).",
                "Kaleido client 'Member' calls http://member.internal:8080 without HTTPS while call credentials are configured: a caller's credential would be sent in clear text. Use an https BaseUrl (Kaleido:Clients:Member:BaseUrl).",
            ],
            logger.Entries.Select(e => e.Message).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("http://localhost:5101/")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://[::1]/")]
    [InlineData("https://member.internal/")]
    [InlineData("HTTPS://member.internal/")]
    public async Task StartAsync_HttpsOrLoopback_DoesNotWarn(string baseUrl)
    {
        await CreateSut(Clients(null, ("Member", baseUrl))).StartAsync(TestContext.Current.CancellationToken);

        Assert.Empty(logger.Entries);
    }

    // A client configured without settings (Kaleido:Clients:Member with no value) uses the shared BaseUrl.
    [Fact]
    public async Task StartAsync_AClientWithoutAnEntry_IsCheckedWithTheSharedBaseUrl()
    {
        var options = new KaleidoClientOptions
        {
            BaseUrl = "http://shared.internal/",
            Clients = new Dictionary<string, KaleidoClientEntry> { ["Member"] = null! },
        };

        await CreateSut(options).StartAsync(TestContext.Current.CancellationToken);

        Assert.Contains("http://shared.internal", Assert.Single(logger.Entries).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_WithoutACallCredentialProvider_DoesNotWarn()
    {
        await CreateSut(Clients(null, ("Member", "http://member.internal/")), withProvider: false)
            .StartAsync(TestContext.Current.CancellationToken);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task StopAsync_Completes()
    {
        await CreateSut(Clients(null)).StopAsync(TestContext.Current.CancellationToken);

        Assert.Empty(logger.Entries);
    }

    private sealed class RecordingLogger : ILogger<KaleidoClientTransportCheck>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
