namespace Kaleido.Http.Client.Processor.UnitTests;

public sealed class KaleidoProcessorClientFactoryTests : Kaleido.UnitTests.SutFixture
{
    private readonly Mock<IKaleidoRegistryTargetResolver> targets = new();
    private readonly Mock<IKaleidoClientTransport> transport = new();

    private KaleidoProcessorClientFactory CreateSut() => new(targets.Object, transport.Object);

    [Fact]
    public void GetClient_CreatesAClientForAnyServiceNameWithoutEndpointConfiguration()
    {
        Assert.IsType<KaleidoProcessorClient>(CreateSut().GetClient("radiology"));
    }

    [Fact]
    public void GetClient_WithAVersion_CreatesAVersionPinnedClient()
    {
        Assert.IsType<KaleidoProcessorClient>(CreateSut().GetClient("radiology", "2026-11"));
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
        Assert.Throws<ArgumentException>(() => CreateSut().GetClient("radiology", " "));
    }
}
