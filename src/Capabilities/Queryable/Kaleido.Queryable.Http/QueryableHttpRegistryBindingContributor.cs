using Kaleido.Registry;

namespace Kaleido.Queryable.Http;

internal sealed class QueryableHttpRegistryBindingContributor : IQueryableRegistryBindingContributor
{
    public QueryableRegistryItem Bind(QueryableRegistryItem item, KaleidoServiceOptions service)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(service);

        var sourceName = item.Name.ToLowerInvariant();
        var serviceName = service.ServiceName;
        var version = service.ServiceVersion;
        return item with
        {
            Bindings = item.Bindings with
            {
                Http = [new RegistryHttpOperation
                {
                    Operation = "query",
                    Method = "POST",
                    Path = QueryableContractUrls.QuerySourceQuery(serviceName, version, sourceName),
                }],
            },
            Views = [.. item.Views.Select(view => view with
            {
                Bindings = view.Bindings with
                {
                    Http = [new RegistryHttpOperation
                    {
                        Operation = "query",
                        Method = "POST",
                        Path = QueryableContractUrls.QueryViewQuery(serviceName, version, sourceName, view.Name.ToLowerInvariant()),
                    }],
                },
            })],
        };
    }
}
