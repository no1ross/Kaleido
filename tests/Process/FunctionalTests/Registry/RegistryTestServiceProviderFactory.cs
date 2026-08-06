using Kaleido.Process.FunctionalTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.FunctionalTests.Tests.Registry;

internal static class RegistryTestServiceProviderFactory
{
    public static IServiceProvider Create(string namespacePrefix)
    {
        var services = new ServiceCollection();

        services.AddKaleido()
            .AddAssembly(typeof(RegistryTestServiceProviderFactory).Assembly)
            .AddParticipant(options =>
            {
                options.TypeFilter =
                    x => x.Namespace is not null &&
                         x.Namespace.StartsWith(
                             namespacePrefix,
                             StringComparison.Ordinal);
            });

        var provider = services.BuildServiceProvider();

        Registry = provider.GetRequiredService<IProcessStepRegistry>();

        return provider;
    }

    public static IProcessStepRegistry? Registry { get; private set; }
}
