namespace Kaleido.Client.Registry.UnitTests;

public sealed class KaleidoRegistryTargetResolverTests : Kaleido.UnitTests.SutFixture
{
    private static readonly RegistryTransportRoute OldRoute = new() { Transport = "http", GatewayRoute = "/clinic/1" };
    private static readonly RegistryTransportRoute CurrentRoute = new() { Transport = "http", GatewayRoute = "/clinic/2" };
    private readonly Mock<IKaleidoRegistrySource> source = new();
    private readonly Mock<IKaleidoTransportSelector> selector = new();
    private readonly KaleidoRegistry registry = new();

    public KaleidoRegistryTargetResolverTests()
    {
        source.Setup(x => x.GetAvailabilityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new KaleidoRegistryAvailability
        {
            Services =
            [
                new RegistryServiceAvailability { ServiceName = "clinic", ServiceVersion = "1", Transports = [OldRoute] },
                new RegistryServiceAvailability { ServiceName = "clinic", ServiceVersion = "2", IsCurrent = true, Transports = [CurrentRoute] },
            ],
        });
        source.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(registry);
        selector.Setup(x => x.Select(It.IsAny<IReadOnlyCollection<RegistryTransportRoute>>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns((IReadOnlyCollection<RegistryTransportRoute> routes, IReadOnlyCollection<string> _) => Assert.Single(routes));
    }

    private KaleidoRegistryTargetResolver CreateSut() => new(source.Object, selector.Object);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WithoutAnExplicitVersion_SelectsTheCurrentService()
    {
        var target = await CreateSut().ResolveAsync("CLINIC", serviceVersion: null, ["http"], Cancel);

        Assert.Same(registry, target.Registry);
        Assert.Equal("2", target.Service.ServiceVersion);
        Assert.Same(CurrentRoute, target.Transport);
    }

    [Fact]
    public async Task AnExplicitVersion_StaysOnThatVersion()
    {
        var target = await CreateSut().ResolveAsync("clinic", "1", ["http"], Cancel);

        Assert.Equal("1", target.Service.ServiceVersion);
        Assert.Same(OldRoute, target.Transport);
    }

    [Fact]
    public async Task AnUnknownService_DoesNotInventARoute()
    {
        var exception = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => CreateSut().ResolveAsync("missing", null, ["http"], Cancel));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, exception.Code);
        selector.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WithoutASupportedTransport_DoesNotInventOne()
    {
        selector.Setup(x => x.Select(It.IsAny<IReadOnlyCollection<RegistryTransportRoute>>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns((RegistryTransportRoute?)null);

        var exception = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => CreateSut().ResolveAsync("clinic", null, ["http"], Cancel));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, exception.Code);
        source.Verify(x => x.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
