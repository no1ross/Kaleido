namespace Kaleido.Queryable.Http.Client.UnitTests;

public sealed class KaleidoQueryableClientFactoryTests : Kaleido.UnitTests.SutFixture
{
    private readonly Mock<IKaleidoRegistryTargetResolver> targets = new();
    private readonly Mock<IHttpClientFactory> clients = new();
    private readonly Mock<IKaleidoInboundCallerAccessor> inboundCaller = new();
    private readonly Mock<IKaleidoCorrelationContextAccessor> correlation = new();

    private KaleidoQueryableClientFactory CreateSut() =>
        new(targets.Object, clients.Object, inboundCaller.Object, correlation.Object, new KaleidoServiceOptions { ServiceName = "reader-service" });

    [Fact]
    public void GetClient_CreatesAClientForAnyServiceNameWithoutEndpointConfiguration()
    {
        Assert.IsType<KaleidoQueryableClient>(CreateSut().GetClient("member"));
    }

    [Fact]
    public void GetClient_WithAVersion_CreatesAVersionPinnedClient()
    {
        Assert.IsType<KaleidoQueryableClient>(CreateSut().GetClient("member", "2026-11"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GetClient_RequiresAServiceName(string serviceName)
    {
        Assert.Throws<ArgumentException>(() => CreateSut().GetClient(serviceName));
    }

    [Fact]
    public void GetClient_WithAVersion_RequiresAVersion()
    {
        Assert.Throws<ArgumentException>(() => CreateSut().GetClient("member", " "));
    }
}
