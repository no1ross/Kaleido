using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.FunctionalTests.Fixtures;

public sealed class RegistryTestFixture
{
    public IServiceProvider ServiceProvider { get; }

    public IProcessStepRegistry? Registry { get; private set; }

    public RegistryTestFixture()
    {
        ServiceProvider = FunctionalTestServiceProviderFactory.Create(
            x => x.Namespace!.StartsWith(FunctionalTestNamespaces.Registry));

        Registry = ServiceProvider.GetService<IProcessStepRegistry>();
    }
}
