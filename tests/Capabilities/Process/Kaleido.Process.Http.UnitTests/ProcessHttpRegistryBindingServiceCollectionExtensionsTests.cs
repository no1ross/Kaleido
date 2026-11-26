using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Http.UnitTests;

public sealed class ProcessHttpRegistryBindingServiceCollectionExtensionsTests : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void AddProcessHttpBindings_RegistersTheActualContributorOnce()
    {
        var services = new ServiceCollection();
        var builder = new Moq.Mock<IKaleidoBuilder>();
        builder.Setup(x => x.Services).Returns(services);

        builder.Object.AddProcessHttpBindings().AddProcessHttpBindings();

        var registration = Assert.Single(services, x => x.ImplementationType == typeof(ProcessHttpRegistryBindingContributor));
        Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);
    }

    [Fact]
    public void AddProcessHttpBindings_RejectsANullBuilder() =>
        Assert.Throws<ArgumentNullException>(() => ProcessHttpRegistryBindingServiceCollectionExtensions.AddProcessHttpBindings(null!));
}
