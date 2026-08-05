using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.FunctionalTests.Fixtures;

public sealed class RuntimeTestFixture
{
    public IServiceProvider ServiceProvider { get; }

    public RuntimeTestFixture()
    {
        var services = new ServiceCollection();

        services.AddKaleido()
            .AddAssembly(typeof(RuntimeTestFixture).Assembly)
            .AddParticipant(options =>
            {
                options.TypeFilter =
                    x => x.Namespace!.StartsWith(
                        FunctionalTestNamespaces.Runtime,
                        StringComparison.Ordinal);
            });

        ServiceProvider =
            services.BuildServiceProvider();
    }
}
