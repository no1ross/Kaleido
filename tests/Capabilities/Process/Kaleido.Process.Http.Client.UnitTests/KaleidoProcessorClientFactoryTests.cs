namespace Kaleido.Process.Http.Client.UnitTests;

public sealed class KaleidoProcessorClientFactoryTests : Kaleido.UnitTests.SutFixture
{
    private static KaleidoProcessorClientFactory CreateSut() => new(
        Mock.Of<IKaleidoRegistryTargetResolver>(),
        Mock.Of<IHttpClientFactory>(),
        Mock.Of<IKaleidoInboundCallerAccessor>(),
        Mock.Of<IKaleidoCorrelationContextAccessor>(),
        new KaleidoServiceOptions { ServiceName = "origin", ServiceVersion = "1" });

    [Fact]
    public void GetClient_WithoutAServiceName_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => CreateSut().GetClient(" "));
    }

    [Fact]
    public void GetClient_WithoutAVersion_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => CreateSut().GetClient("radiology", " "));
    }

    [Fact]
    public void GetClient_CurrentAndPinnedVersions_ReturnClients()
    {
        var factory = CreateSut();

        Assert.IsType<KaleidoProcessorClient>(factory.GetClient("radiology"));
        Assert.IsType<KaleidoProcessorClient>(factory.GetClient("radiology", "2"));
    }
}
