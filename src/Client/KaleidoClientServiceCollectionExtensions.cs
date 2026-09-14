using Kaleido.Client;
using Kaleido.Process.AspNetCore.Client;
using Kaleido.Queryable.AspNetCore.Client;
using Microsoft.Extensions.Configuration;

namespace Kaleido;

public static class KaleidoClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers named Kaleido downstream clients.
    /// For each name, both a process client and a queryable client are registered.
    /// The framework determines at registry call time which capabilities each service
    /// actually exposes.
    /// </summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <param name="clientNames">
    /// The names of the downstream services to register clients for
    /// (e.g. <c>"Member"</c>, <c>"CodeSet"</c>).
    /// </param>
    /// <remarks>
    /// <para>
    /// Base URL resolution order for each name:
    /// <list type="number">
    ///   <item><c>Kaleido:Clients:&lt;Name&gt;:BaseUrl</c> — per-client override</item>
    ///   <item><c>Kaleido:Clients:BaseUrl</c> — shared default (typically the router)</item>
    /// </list>
    /// </para>
    /// <para>
    /// Route prefix resolution order:
    /// <list type="number">
    ///   <item><c>Kaleido:Clients:&lt;Name&gt;:RoutePrefix</c> — explicit override</item>
    ///   <item>The client name lowercased — e.g. <c>"Member"</c> → <c>"member"</c></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static IKaleidoBuilder AddKaleidoClients(
        this IKaleidoBuilder builder,
        params string[] clientNames)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new KaleidoClientOptions();
        var section = builder.Configuration.GetSection(KaleidoOptions.SectionName);
        section.Bind(options);

        foreach (var name in clientNames)
        {
            options.Clients.TryGetValue(name, out var entry);

            var baseUrl = !string.IsNullOrWhiteSpace(entry?.BaseUrl)
                ? entry.BaseUrl
                : options.BaseUrl;

            if (string.IsNullOrWhiteSpace(baseUrl))
                continue;

            var prefix = entry?.RoutePrefix ?? name.ToLowerInvariant();

            builder.AddProcessClient(o =>
            {
                o.Name = name;
                o.BaseUrl = baseUrl;
                o.RoutePrefix = prefix;
            });

            builder.AddQueryableClient(o =>
            {
                o.Name = name;
                o.BaseUrl = baseUrl;
                o.RoutePrefix = prefix;
            });
        }

        return builder;
    }
}
