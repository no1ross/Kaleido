using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Http.Client.UnitTests;

public sealed class ProcessHttpClientServiceCollectionExtensionsTests : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void AddProcessHttpClient_RegistersTheFactoryOnceWithoutAutoParticipation()
    {
        var services = new ServiceCollection();
        var builder = new Mock<IKaleidoBuilder>();
        builder.Setup(x => x.Services).Returns(services);
        builder.Setup(x => x.Configuration).Returns(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "https://gateway.test/",
        }).Build());

        builder.Object.AddProcessHttpClient().AddProcessHttpClient();

        Assert.Single(services, x => x.ServiceType == typeof(IKaleidoProcessorClientFactory) && x.ImplementationType == typeof(KaleidoProcessorClientFactory));
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IKaleidoRegistryClient));
    }
}
