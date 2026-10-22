using Kaleido.Http.Mcp.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class ToolCatalogProviderTests
    : Kaleido.UnitTests.SutFixture
{
    // Translations are reused per registry revision across instances, so every test uses its own.
    private readonly string revision = $"r-{Guid.NewGuid():N}";
    private readonly Mock<IKaleidoRegistryService> registry = new();
    private readonly Mock<IToolCatalogBuilder> builder = new();
    private readonly Mock<IMcpObservability> observability = new();

    public ToolCatalogProviderTests()
    {
        registry.Setup(x => x.IsMapped).Returns(true);
        registry.Setup(x => x.GetForCallerAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AggregatedRegistryResponse { Revision = revision });
        builder.Setup(x => x.Build(It.IsAny<AggregatedRegistryResponse>()))
            .Returns((AggregatedRegistryResponse response) => new ToolCatalog { Revision = response.Revision });
    }

    private ToolCatalogProvider CreateSut(McpToolListing listing = McpToolListing.All, ILogger<ToolCatalogProvider>? logger = null) =>
        new(
            registry.Object,
            builder.Object,
            observability.Object,
            new KaleidoMcpOptions { Instructions = "test", ToolListing = listing },
            logger ?? NullLogger<ToolCatalogProvider>.Instance);

    private sealed class RecordingLogger : ILogger<ToolCatalogProvider>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private void RegistryRevisionIs(string? value) =>
        registry.Setup(x => x.GetForCallerAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AggregatedRegistryResponse { Revision = value });

    [Fact]
    public async Task GetAsync_ReportsWhatTheOwnerShouldFix_AsWarnings_OncePerRevision()
    {
        var logger = new RecordingLogger();
        builder.Setup(x => x.Build(It.IsAny<AggregatedRegistryResponse>()))
            .Returns(new ToolCatalog
            {
                MissingDescriptions = ["clinic_patient", "clinic_patient.name"],
                TruncatedNames = ["clinic_a_very_long_name"],
                NameCollisions = ["Patient -> clinic_patient_query"],
                UnresolvedLookups = ["clinic_pick.patientId -> clinic/Patient"],
                UnknownListedTools = ["clinic_missing"],
                IsPartial = true,
                UnavailableServices = ["radiology", "billing"]
            });

        await CreateSut(logger: logger).GetAsync(Cancel);
        await CreateSut(logger: logger).GetAsync(Cancel);
        var firstRevision = logger.Entries.ToArray();
        RegistryRevisionIs($"r-{Guid.NewGuid():N}");
        await CreateSut(logger: logger).GetAsync(Cancel);

        Assert.All(logger.Entries, x => Assert.Equal(LogLevel.Warning, x.Level));
        Assert.Equal(firstRevision.Concat(firstRevision), logger.Entries);
        Assert.Equal(
            [
                "Kaleido MCP: 2 tool(s) or argument(s) have no description, so agents must guess what they mean. Add [Description] to: clinic_patient, clinic_patient.name.",
                "Kaleido MCP: 1 tool name(s) exceeded MCP's 64-character limit and were shortened; rename the capability types (KAL2016): clinic_a_very_long_name.",
                "Kaleido MCP: 1 capability name(s) collide, so their tools carry a kind suffix; rename the capability types so each name says what it is: Patient -> clinic_patient_query.",
                "Kaleido MCP: 1 step input lookup(s) name a query that is not published or not visible to this caller, so agents must ask for the value: clinic_pick.patientId -> clinic/Patient.",
                "Kaleido MCP: 1 name(s) in Kaleido:Mcp:ListedTools are not tools of this caller, so they are not listed (check the names, or the caller's access): clinic_missing.",
                "Kaleido MCP: the registry is partial; tools from radiology, billing are missing until those services respond."
            ],
            firstRevision.Select(x => x.Message));
    }

    [Fact]
    public async Task GetAsync_WithWarningsDisabled_WritesNoReport()
    {
        var logger = new Mock<ILogger<ToolCatalogProvider>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(false);
        builder.Setup(x => x.Build(It.IsAny<AggregatedRegistryResponse>()))
            .Returns(new ToolCatalog
            {
                MissingDescriptions = ["clinic_patient"],
                TruncatedNames = ["clinic_a_very_long_name"],
                NameCollisions = ["Patient -> clinic_patient_query"],
                UnresolvedLookups = ["clinic_pick.patientId -> clinic/Patient"],
                UnknownListedTools = ["clinic_missing"],
                IsPartial = true
            });

        await CreateSut(logger: logger.Object).GetAsync(Cancel);

        logger.Verify(x => x.IsEnabled(LogLevel.Warning), Times.Exactly(6));
        Assert.DoesNotContain(logger.Invocations, x => x.Method.Name == nameof(ILogger.Log));
    }

    [Fact]
    public async Task GetAsync_ACompleteCatalog_ReportsNothing()
    {
        var logger = new RecordingLogger();

        await CreateSut(logger: logger).GetAsync(Cancel);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task GetAsync_ARegistryWithoutARevision_IsReadAsTheEmptyRevision()
    {
        RegistryRevisionIs(null);

        var catalog = await CreateSut(McpToolListing.FrontDoor).GetAsync(Cancel);

        Assert.Null(catalog.Revision);
        observability.Verify(x => x.RegistryRead(It.IsIn(McpTelemetry.RegistryFetched, McpTelemetry.RegistryHit), string.Empty));
    }

    [Fact]
    public async Task GetAsync_KeepsABoundedNumberOfTranslations_SoAnOldRevisionIsTranslatedAgain()
    {
        await CreateSut().GetAsync(Cancel);

        for (var i = 0; i < 256; i++)
        {
            RegistryRevisionIs($"r-{Guid.NewGuid():N}");
            await CreateSut().GetAsync(Cancel);
        }

        RegistryRevisionIs(revision);
        await CreateSut().GetAsync(Cancel);

        builder.Verify(x => x.Build(It.Is<AggregatedRegistryResponse>(r => r.Revision == revision)), Times.Exactly(2));
        observability.Verify(x => x.RegistryRead(McpTelemetry.RegistryHit, revision), Times.Never);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_TranslatesTheRegistryTheCallerMaySee()
    {
        var catalog = await CreateSut().GetAsync(Cancel);

        Assert.Equal(revision, catalog.Revision);
        registry.Verify(x => x.GetForCallerAsync(false, It.IsAny<CancellationToken>()));
        observability.Verify(x => x.RegistryRead(McpTelemetry.RegistryFetched, revision));
    }

    [Fact]
    public async Task GetAsync_TheSameRevision_ReusesTheTranslation_ButAlwaysAsksTheRegistry()
    {
        var first = await CreateSut().GetAsync(Cancel);
        var second = await CreateSut().GetAsync(Cancel);

        Assert.Same(first, second);
        builder.Verify(x => x.Build(It.IsAny<AggregatedRegistryResponse>()), Times.Once);
        registry.Verify(x => x.GetForCallerAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        observability.Verify(x => x.RegistryRead(McpTelemetry.RegistryHit, revision));
    }

    [Fact]
    public async Task GetAsync_ADifferentListing_IsTranslatedAgain_ForTheSameRevision()
    {
        var all = await CreateSut(McpToolListing.All).GetAsync(Cancel);
        var frontDoor = await CreateSut(McpToolListing.FrontDoor).GetAsync(Cancel);

        Assert.NotSame(all, frontDoor);
        builder.Verify(x => x.Build(It.IsAny<AggregatedRegistryResponse>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetAsync_ADifferentRevision_IsTranslatedAgain()
    {
        await CreateSut().GetAsync(Cancel);

        var other = $"r-{Guid.NewGuid():N}";
        registry.Setup(x => x.GetForCallerAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AggregatedRegistryResponse { Revision = other });

        var catalog = await CreateSut().GetAsync(Cancel);

        Assert.Equal(other, catalog.Revision);
        builder.Verify(x => x.Build(It.IsAny<AggregatedRegistryResponse>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetAsync_BeforeTheRegistryIsMapped_IsUnavailable_AndReadsNothing()
    {
        registry.Setup(x => x.IsMapped).Returns(false);

        var catalog = await CreateSut().GetAsync(Cancel);

        Assert.Contains("MapKaleidoHttp", catalog.Unavailable, StringComparison.Ordinal);
        Assert.Empty(catalog.Tools);
        registry.Verify(x => x.GetForCallerAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
