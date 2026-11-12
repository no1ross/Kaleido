using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Hosting;

internal sealed class KaleidoProviderRegistrationCheck(IServiceCollection services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        if (services.Any(x => x.ServiceType == typeof(KaleidoProcessOptions)))
        {
            Require<IProcessContextStore>(services, missing, "process state (IProcessContextStore)");
        }

        if (services.Any(x => x.ServiceType == typeof(KaleidoEventOptions)))
        {
            Require<IKaleidoOutboxStore>(services, missing, "event outbox (IKaleidoOutboxStore)");
        }

        if (missing.Count > 0)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.StoresNotChosen,
                $"No provider is registered for {string.Join(", ", missing)}. Register durable stores or call AddInMemoryProviders() for development and tests.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void Require<TStore>(IServiceCollection services, List<string> missing, string name)
    {
        if (!services.Any(x => x.ServiceType == typeof(TStore)))
        {
            missing.Add(name);
        }
    }
}
