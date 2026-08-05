using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.FunctionalTests.Fixtures;

public sealed class RegistryTestFixture
{
    public IServiceProvider ServiceProvider { get; }

    public RegistryTestFixture()
    {
        ServiceProvider = FunctionalTestServiceProviderFactory.Create(
            x => x.Namespace!.StartsWith(FunctionalTestNamespaces.Registry));
    }
}
