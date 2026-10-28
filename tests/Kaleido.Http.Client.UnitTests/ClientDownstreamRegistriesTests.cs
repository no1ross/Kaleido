using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Client.UnitTests;

public sealed class ClientDownstreamRegistriesTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly List<KaleidoClientDefinition> clients = [];
    private readonly Mock<IKaleidoRemoteRegistry> remoteRegistry = new();
    private readonly ICorrelationHeaderStamper stamper = Mock.Of<ICorrelationHeaderStamper>();

    private void Client(string name, string routePrefix, Exception? failure = null)
    {
        var client = new KaleidoClientDefinition(name, routePrefix, RegistryTtl: null, StrictRegistryProbe: false, HealthCheckTimeout: TimeSpan.FromSeconds(5));
        clients.Add(client);

        var registry = remoteRegistry.Setup(x => x.GetAsync(client, stamper, It.IsAny<CancellationToken>()));

        if (failure is not null)
        {
            registry.ThrowsAsync(failure);
        }
        else
        {
            registry.ReturnsAsync(new AggregatedRegistryResponse
            {
                Processes = [new ProcessorRegistryResponse { Name = routePrefix, ServiceName = routePrefix }],
                Queryables = [new QueryableSourceResponse { Name = $"{routePrefix}-source", ServiceName = routePrefix }],
            });
        }
    }

    private ClientDownstreamRegistries CreateSut(
        string serviceName = "router",
        ILogger<ClientDownstreamRegistries>? logger = null)
    {
        var catalog = new Mock<IKaleidoClientCatalog>();
        catalog.Setup(x => x.Clients).Returns(clients);

        return new ClientDownstreamRegistries(
            catalog.Object,
            remoteRegistry.Object,
            stamper,
            new KaleidoServiceOptions { ServiceName = serviceName },
            logger ?? NullLogger<ClientDownstreamRegistries>.Instance);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_ReturnsEveryClientsRegistry()
    {
        Client("Intake", "intake");
        Client("Radiology", "radiology");

        var registries = await CreateSut().GetAsync(maxParallelism: null, cancellationToken: Cancel);

        Assert.Equal(["intake", "radiology"], registries.Processes.Select(x => x.Name).Order());
        Assert.Equal(["intake-source", "radiology-source"], registries.Queryables.Select(x => x.Name).Order());
        Assert.Empty(registries.Errors);
    }

    [Fact]
    public async Task GetAsync_SkipsAClientThatIsThisService_SoAnAggregatorNeverFetchesItself()
    {
        Client("Self", "router");
        Client("Intake", "intake");

        var registries = await CreateSut("router").GetAsync(maxParallelism: null, cancellationToken: Cancel);

        Assert.Equal(["intake"], registries.Processes.Select(x => x.Name));
        remoteRegistry.Verify(x => x.GetAsync(It.Is<KaleidoClientDefinition>(c => c.Name == "Self"), It.IsAny<ICorrelationHeaderStamper>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_AFailingClient_IsReported_AndTheOthersAreStillReturned()
    {
        Client("Intake", "intake");
        Client("Radiology", "radiology", failure: new HttpRequestException("down"));

        var registries = await CreateSut().GetAsync(maxParallelism: 1, cancellationToken: Cancel);

        Assert.Equal(["intake"], registries.Processes.Select(x => x.Name));
        var error = Assert.Single(registries.Errors);
        Assert.Equal("Radiology", error.ClientName);
        Assert.DoesNotContain("down", error.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_TheCallersCancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Client("Intake", "intake", failure: new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSut().GetAsync(maxParallelism: null, cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task GetAsync_AClientThatTimesOut_IsReported_WhenTheCallerDidNotCancel()
    {
        Client("Intake", "intake");
        Client("Radiology", "radiology", failure: new TaskCanceledException("timed out"));

        var registries = await CreateSut().GetAsync(maxParallelism: null, cancellationToken: Cancel);

        Assert.Equal(["intake"], registries.Processes.Select(x => x.Name));
        Assert.Equal("Radiology", Assert.Single(registries.Errors).ClientName);
    }

    [Fact]
    public async Task GetAsync_AFailingClient_IsLoggedAsAWarningWithTheFailure()
    {
        var logger = new RecordingLogger();
        Client("Intake", "intake", failure: new HttpRequestException("intake down"));
        Client("Radiology", "radiology", failure: new HttpRequestException("radiology down"));

        var registries = await CreateSut(logger: logger).GetAsync(maxParallelism: null, cancellationToken: Cancel);

        Assert.Equal(["Intake", "Radiology"], registries.Errors.Select(e => e.ClientName).Order());
        Assert.Equal(
            ["Registry client Intake failed: intake down.", "Registry client Radiology failed: radiology down."],
            logger.Entries.Select(e => e.Message).Order());
        Assert.All(logger.Entries, e =>
        {
            Assert.Equal(LogLevel.Warning, e.Level);
            Assert.IsType<HttpRequestException>(e.Exception);
        });
    }

    [Fact]
    public async Task InvalidateAsync_DropsEachClientsCachedRegistry_ExceptThisServices()
    {
        Client("Self", "router");
        Client("Intake", "intake");

        await CreateSut("router").InvalidateAsync(Cancel);

        remoteRegistry.Verify(x => x.InvalidateAsync("Intake", Cancel), Times.Once);
        remoteRegistry.Verify(x => x.InvalidateAsync("Self", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvalidateAsync_AFailingClient_DoesNotStopTheOthers()
    {
        Client("Intake", "intake");
        Client("Radiology", "radiology");
        remoteRegistry.Setup(x => x.InvalidateAsync("Intake", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("store down"));

        await CreateSut().InvalidateAsync(Cancel);

        remoteRegistry.Verify(x => x.InvalidateAsync("Radiology", Cancel), Times.Once);
    }

    [Fact]
    public async Task InvalidateAsync_AFailingClient_IsLoggedAsAWarning()
    {
        var logger = new RecordingLogger();
        Client("Intake", "intake");
        Client("Radiology", "radiology");
        remoteRegistry.Setup(x => x.InvalidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("store down"));

        await CreateSut(logger: logger).InvalidateAsync(Cancel);

        Assert.Equal(
            ["Failed to invalidate registry cache for client Intake.", "Failed to invalidate registry cache for client Radiology."],
            logger.Entries.Select(e => e.Message).Order());
        Assert.All(logger.Entries, e => Assert.Equal(LogLevel.Warning, e.Level));
    }

    [Fact]
    public async Task InvalidateAsync_TheCallersCancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Client("Intake", "intake");
        remoteRegistry.Setup(x => x.InvalidateAsync("Intake", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSut().InvalidateAsync(cancellation.Token));
    }

    private sealed class RecordingLogger : ILogger<ClientDownstreamRegistries>
    {
        public System.Collections.Concurrent.ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Enqueue((logLevel, formatter(state, exception), exception));
    }
}
