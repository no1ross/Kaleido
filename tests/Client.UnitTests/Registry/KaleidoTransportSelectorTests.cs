namespace Kaleido.Client.Registry.UnitTests;

public sealed class KaleidoTransportSelectorTests : Kaleido.UnitTests.SutFixture
{
    private static readonly RegistryTransportRoute Http = new() { Transport = "http", GatewayRoute = "/clinic/1" };
    private static readonly RegistryTransportRoute Grpc = new() { Transport = "grpc", GatewayRoute = "/grpc/clinic/1" };

    private static KaleidoTransportSelector CreateSut() => new();

    [Fact]
    public void Select_PrefersGrpcWhenTheHostImplementsBoth()
    {
        Assert.Same(Grpc, CreateSut().Select([Http, Grpc], ["http", "grpc"]));
    }

    [Fact]
    public void Select_UsesThePublishedHttpRouteWhenOnlyHttpIsImplemented()
    {
        Assert.Same(Http, CreateSut().Select([Grpc, Http], ["http"]));
    }

    [Fact]
    public void Select_WithoutACommonTransport_ReturnsNone()
    {
        Assert.Null(CreateSut().Select([Grpc], ["http"]));
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    public void Fallback_OffersHttpOnlyWhenTheAttemptCanBeSafelyRepeated(bool sent, bool read, bool keyed, bool allowed)
    {
        var fallback = CreateSut().Fallback([Grpc, Http], ["grpc", "http"], "grpc", sent, read, keyed);

        Assert.Equal(allowed ? Http : null, fallback);
    }

    [Fact]
    public void Fallback_WithoutAPublishedAlternative_ReturnsNone()
    {
        Assert.Null(CreateSut().Fallback([Grpc], ["grpc", "http"], "grpc", wasSent: false, isRead: false, hasIdempotencyKey: false));
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    public void CanFallback_RequiresNoSentRequest_ARead_OrAnIdempotencyKey(bool sent, bool read, bool keyed, bool allowed)
    {
        Assert.Equal(allowed, CreateSut().CanFallback(sent, read, keyed));
    }
}
