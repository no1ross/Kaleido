using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Client.Queryable;

// For a client name chosen at runtime (an MCP tool call, the registry aggregation): the configured
// client of that name, as the container built it (a keyed service, #498). Code that knows the name
// injects [FromKeyedServices("name")] IKaleidoQueryableClient instead.
internal sealed class KaleidoQueryableClientFactory(
    IServiceProvider services,
    IKaleidoClientCatalog catalog)
    : IKaleidoQueryableClientFactory
{
    public IKaleidoQueryableClient GetClient(string name) =>
        services.GetRequiredKeyedService<IKaleidoQueryableClient>(catalog.Require(name).Name);

    public bool TryGetClientForService(string serviceName, [NotNullWhen(true)] out IKaleidoQueryableClient? client)
    {
        client = catalog.FindByService(serviceName) is { } found
            ? services.GetRequiredKeyedService<IKaleidoQueryableClient>(found.Name)
            : null;

        return client is not null;
    }
}
