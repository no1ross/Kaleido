using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Client.Processor;

// For a client name chosen at runtime (an MCP tool call, the registry aggregation): the configured
// client of that name, as the container built it (a keyed service, #498). Code that knows the name
// injects [FromKeyedServices("name")] IKaleidoProcessorClient instead.
internal sealed class KaleidoProcessorClientFactory(
    IServiceProvider services,
    IKaleidoClientCatalog catalog)
    : IKaleidoProcessorClientFactory
{
    public IKaleidoProcessorClient GetClient(string name) =>
        services.GetRequiredKeyedService<IKaleidoProcessorClient>(catalog.Require(name).Name);

    public bool TryGetClientForService(string serviceName, [NotNullWhen(true)] out IKaleidoProcessorClient? client)
    {
        client = catalog.FindByService(serviceName) is { } found
            ? services.GetRequiredKeyedService<IKaleidoProcessorClient>(found.Name)
            : null;

        return client is not null;
    }
}
