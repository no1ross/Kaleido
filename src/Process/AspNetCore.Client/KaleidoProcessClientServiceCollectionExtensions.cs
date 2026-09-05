using Kaleido.Process.AspNetCore.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido;

public static class KaleidoProcessClientServiceCollectionExtensions
{
    public static IKaleidoBuilder AddProcessClient(
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

        builder.Services.TryAddScoped<IKaleidoProcessClientFactory, KaleidoProcessClientFactory>();

        return builder;
    }
}
