using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.UnitTests;

public class KaleidoTestFixture
{
    public Mock<IServiceScopeFactory> ScopeFactory { get; } = new();

    public Mock<IServiceScope> Scope { get; } = new();

    public Mock<IServiceProvider> ServiceProvider { get; } = new();

    public Mock<IRecordRegistry> Registry { get; } = new();

    public Mock<IRecordDescriptorFactory> Descriptors { get; } = new();

    public Mock<IRecordDispatcher> Dispatcher { get; } = new();

    public KaleidoTestFixture()
    {
        Scope
            .SetupGet(x => x.ServiceProvider)
            .Returns(ServiceProvider.Object);

        ScopeFactory
            .Setup(x => x.CreateScope())
            .Returns(Scope.Object);
    }

    public void Reset()
    {
        ScopeFactory.Reset();
        Scope.Reset();
        ServiceProvider.Reset();
        Registry.Reset();
        Descriptors.Reset();
        Dispatcher.Reset();

        Scope
            .SetupGet(x => x.ServiceProvider)
            .Returns(ServiceProvider.Object);

        ScopeFactory
            .Setup(x => x.CreateScope())
            .Returns(Scope.Object);
    }

    public void ConfigureScopeService(
        Type serviceType,
        object service)
    {
        ServiceProvider
            .Setup(x => x.GetService(serviceType))
            .Returns(service);
    }
}
