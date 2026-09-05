using Kaleido.Queryable.AspNetCore.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido;

public static class KaleidoQueryableClientServiceCollectionExtensions
{
    public static IKaleidoBuilder AddQueryableClient(
        this IKaleidoBuilder builder,
        string name,
        string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        builder.Services.AddHttpClient(name, client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });

        builder.Services.TryAddScoped<IKaleidoQueryableClientFactory, KaleidoQueryableClientFactory>();

        return builder;
    }
}
