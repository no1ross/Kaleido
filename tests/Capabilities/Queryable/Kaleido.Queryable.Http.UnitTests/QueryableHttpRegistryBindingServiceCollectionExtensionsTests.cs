using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.Http.UnitTests;

public sealed class QueryableHttpRegistryBindingServiceCollectionExtensionsTests : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void AddQueryableHttpBindings_RegistersTheActualContributorOnce()
    {
        var services = new ServiceCollection();
        var builder = new Moq.Mock<IKaleidoBuilder>();
        builder.Setup(x => x.Services).Returns(services);

        builder.Object.AddQueryableHttpBindings().AddQueryableHttpBindings();

        var registration = Assert.Single(services, x => x.ImplementationType == typeof(QueryableHttpRegistryBindingContributor));
        Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);
    }

    [Fact]
    public void AddQueryableHttpBindings_RejectsANullBuilder() =>
        Assert.Throws<ArgumentNullException>(() => QueryableHttpRegistryBindingServiceCollectionExtensions.AddQueryableHttpBindings(null!));
}
