using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.Http.Client.UnitTests;

public sealed class QueryableHttpClientServiceCollectionExtensionsTests : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void AddQueryableHttpClient_RegistersTheFactoryOnceWithoutAutoParticipation()
    {
        var services = new ServiceCollection();
        var builder = new Mock<IKaleidoBuilder>();
        builder.Setup(x => x.Services).Returns(services);
        builder.Setup(x => x.Configuration).Returns(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "https://gateway.test/",
        }).Build());

        builder.Object.AddQueryableHttpClient().AddQueryableHttpClient();

        Assert.Single(services, x => x.ServiceType == typeof(IKaleidoQueryableClientFactory) && x.ImplementationType == typeof(KaleidoQueryableClientFactory));
        var keyed = Assert.Single(services, x => x.ServiceType == typeof(IKaleidoQueryableClient) && x.ServiceKey == KeyedService.AnyKey);
        Assert.Equal(ServiceLifetime.Scoped, keyed.Lifetime);
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IKaleidoRegistryClient));
    }
}
