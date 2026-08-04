using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.FunctionalTests.Fixtures;

public static class FunctionalTestServiceProviderFactory
{
    public static IServiceProvider Create(Func<Type, bool>? typeFilter = null)
    {
        var services = new ServiceCollection();

        services.AddKaleido()
            .AddAssembly(typeof(FunctionalTestServiceProviderFactory).Assembly)
            .AddParticipant(options =>
            {
                options.TypeFilter = typeFilter;
            });

        return services.BuildServiceProvider();
    }
}
