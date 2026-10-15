using Kaleido.Http.Mcp.Observability;
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

    private ToolCatalogProvider CreateSut(McpToolListing listing = McpToolListing.All) =>
        new(
            registry.Object,
            builder.Object,
            observability.Object,
            new KaleidoMcpOptions { Instructions = "test", ToolListing = listing },
            NullLogger<ToolCatalogProvider>.Instance);

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
