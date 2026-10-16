using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Client.UnitTests;

public sealed class ClientDownstreamRegistriesTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly KaleidoProcessorClientRouteOptionsMap processMap = new();
    private readonly KaleidoQueryableClientRouteOptionsMap queryableMap = new();
    private readonly Dictionary<string, Mock<IKaleidoProcessorClient>> processClients = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Mock<IKaleidoQueryableClient>> queryableClients = new(StringComparer.OrdinalIgnoreCase);

    private void Client(string name, string routePrefix, Exception? failure = null)
    {
        processMap.SetRoutePrefix(name, routePrefix);
        queryableMap.SetRoutePrefix(name, routePrefix);

        var process = new Mock<IKaleidoProcessorClient>();
        var queryable = new Mock<IKaleidoQueryableClient>();

        if (failure is not null)
        {
            process.Setup(x => x.GetRegistryAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        }
        else
        {
            process.Setup(x => x.GetRegistryAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([new ProcessorRegistryResponse { Name = routePrefix, ServiceName = routePrefix }]);
            queryable.Setup(x => x.GetRegistryAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([new QueryableSourceResponse { Name = $"{routePrefix}-source", ServiceName = routePrefix }]);
        }

        processClients[name] = process;
        queryableClients[name] = queryable;
    }

    private ClientDownstreamRegistries CreateSut(
        string serviceName = "router",
        ILogger<ClientDownstreamRegistries>? logger = null)
    {
        var processFactory = new Mock<IKaleidoProcessorClientFactory>();
        processFactory.Setup(x => x.GetClient(It.IsAny<string>())).Returns((string name) => processClients[name].Object);

        var queryableFactory = new Mock<IKaleidoQueryableClientFactory>();
        queryableFactory.Setup(x => x.GetClient(It.IsAny<string>())).Returns((string name) => queryableClients[name].Object);

        return new ClientDownstreamRegistries(
            processMap,
            queryableMap,
            processFactory.Object,
            queryableFactory.Object,
            new KaleidoServiceOptions { ServiceName = serviceName },
            logger ?? NullLogger<ClientDownstreamRegistries>.Instance);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_ReturnsEveryClientsRegistry()
    {
        Client("Intake", "intake");
        Client("Radiology", "radiology");

        var registries = await CreateSut().GetAsync(cancellationToken: Cancel);

        Assert.Equal(["intake", "radiology"], registries.Processes.Select(x => x.Name).Order());
        Assert.Equal(["intake-source", "radiology-source"], registries.Queryables.Select(x => x.Name).Order());
        Assert.Empty(registries.Errors);
    }

    [Fact]
    public async Task GetAsync_SkipsAClientThatIsThisService_SoAnAggregatorNeverFetchesItself()
    {
        Client("Self", "router");
        Client("Intake", "intake");

        var registries = await CreateSut("router").GetAsync(cancellationToken: Cancel);

        Assert.Equal(["intake"], registries.Processes.Select(x => x.Name));
        processClients["Self"].Verify(x => x.GetRegistryAsync(It.IsAny<CancellationToken>()), Times.Never);
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

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSut().GetAsync(cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task InvalidateAsync_DropsEachClientsCachedRegistry_ExceptThisServices()
    {
        Client("Self", "router");
        Client("Intake", "intake");

        await CreateSut("router").InvalidateAsync(Cancel);

        processClients["Intake"].Verify(x => x.InvalidateRegistryAsync(Cancel), Times.Once);
        processClients["Self"].Verify(x => x.InvalidateRegistryAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvalidateAsync_AQueryableOnlyClient_IsInvalidatedThroughItsQueryableClient()
    {
        queryableMap.SetRoutePrefix("Catalog", "catalog");
        queryableClients["Catalog"] = new Mock<IKaleidoQueryableClient>();

        await CreateSut().InvalidateAsync(Cancel);

        queryableClients["Catalog"].Verify(x => x.InvalidateRegistryAsync(Cancel), Times.Once);
    }

    [Fact]
    public async Task InvalidateAsync_AFailingClient_DoesNotStopTheOthers()
    {
        Client("Intake", "intake");
        Client("Radiology", "radiology");
        processClients["Intake"].Setup(x => x.InvalidateRegistryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("store down"));

        await CreateSut().InvalidateAsync(Cancel);

        processClients["Radiology"].Verify(x => x.InvalidateRegistryAsync(Cancel), Times.Once);
    }

    [Fact]
    public async Task GetAsync_AClientThatTimesOut_IsReported_WhenTheCallerDidNotCancel()
    {
        Client("Intake", "intake");
        Client("Radiology", "radiology", failure: new TaskCanceledException("timed out"));

        var registries = await CreateSut().GetAsync(cancellationToken: Cancel);

        Assert.Equal(["intake"], registries.Processes.Select(x => x.Name));
        Assert.Equal("Radiology", Assert.Single(registries.Errors).ClientName);
    }

    [Fact]
    public async Task GetAsync_AFailingClient_IsLoggedAsAWarningWithTheFailure()
    {
        var logger = new RecordingLogger();
        Client("Intake", "intake", failure: new HttpRequestException("intake down"));
        Client("Radiology", "radiology", failure: new HttpRequestException("radiology down"));

        var registries = await CreateSut(logger: logger).GetAsync(cancellationToken: Cancel);

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
    public async Task InvalidateAsync_AFailingClient_IsLoggedAsAWarning()
    {
        var logger = new RecordingLogger();
        Client("Intake", "intake");
        Client("Radiology", "radiology");
        processClients["Intake"].Setup(x => x.InvalidateRegistryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("store down"));
        processClients["Radiology"].Setup(x => x.InvalidateRegistryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("store down"));

        await CreateSut(logger: logger).InvalidateAsync(Cancel);

        Assert.Equal(
            ["Failed to invalidate registry cache for client Intake.", "Failed to invalidate registry cache for client Radiology."],
            logger.Entries.Select(e => e.Message).Order());
        Assert.All(logger.Entries, e => Assert.Equal(LogLevel.Warning, e.Level));
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

    [Fact]
    public async Task InvalidateAsync_TheCallersCancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Client("Intake", "intake");
        processClients["Intake"].Setup(x => x.InvalidateRegistryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSut().InvalidateAsync(cancellation.Token));
    }
}
