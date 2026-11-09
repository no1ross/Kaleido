namespace Kaleido.Http.Client.Queryable.UnitTests;

public sealed class KaleidoQueryableClientFactoryTests : Kaleido.UnitTests.SutFixture
{
    private readonly Mock<IKaleidoRegistryTargetResolver> targets = new();
    private readonly Mock<IKaleidoClientTransport> transport = new();

    private KaleidoQueryableClientFactory CreateSut() => new(targets.Object, transport.Object);

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
