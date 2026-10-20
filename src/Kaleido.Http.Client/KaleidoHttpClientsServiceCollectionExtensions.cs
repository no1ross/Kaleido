using Kaleido.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client;

/// <summary>
/// Registers the typed Kaleido HTTP clients on an <see cref="IKaleidoBuilder"/>; the entry point is
/// <see cref="AddHttpClients"/>.
/// </summary>
public static class KaleidoHttpClientsServiceCollectionExtensions
{
    /// <summary>
    /// Registers named Kaleido Process and Queryable clients from configuration.
    /// Reads <c>Kaleido:Clients</c> configuration section and registers both Process and Queryable clients
    /// for each configured client name.
    /// </summary>
    public static IKaleidoBuilder AddHttpClients(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var config = new KaleidoClientOptions();
        builder.Configuration.GetSection(KaleidoServiceOptions.SectionName).Bind(config);
        builder.Services.AddSingleton(config);

        foreach (var (name, entry) in config.Clients)
        {
            AddClients(builder, config, name, entry);
        }

        // An aggregating host merges these clients' registries into its own (AddHttp() registers
        // a "no downstream" default; with clients configured, this replaces it).
        if (config.Clients.Count > 0)
        {
            builder.Services.Replace(
                ServiceDescriptor.Scoped<IKaleidoDownstreamRegistries, ClientDownstreamRegistries>());

            // With an IKaleidoCallCredentialProvider registered, it decides every call's Authorization
            // (innermost handler); without one the host's own client handlers do, as before.
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, KaleidoCallCredentialHandlerFilter>());
        }

        return builder;
    }

    /// <summary>
    /// Registers <typeparamref name="TProvider"/> as the <see cref="IKaleidoCallCredentialProvider"/>
    /// (singleton): it decides the <c>Authorization</c> of every Kaleido client call, for the caller or
    /// for the service itself (ADR 0022). Replaces a provider registered before.
    /// </summary>
    /// <typeparam name="TProvider">The host's provider, e.g. an OAuth on-behalf-of token exchange.</typeparam>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder.</returns>
    public static IKaleidoBuilder AddCallCredentials<TProvider>(this IKaleidoBuilder builder)
        where TProvider : class, IKaleidoCallCredentialProvider
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Replace(ServiceDescriptor.Singleton<IKaleidoCallCredentialProvider, TProvider>());
        return builder;
    }

    /// <summary>
    /// Sends a caller's own credential unchanged on every call made for that caller, and keeps the host's
    /// configured credentials on the service's own calls (registry fetches): for deployments where every
    /// service accepts the caller's token (one audience). Where services have their own audiences,
    /// register a provider that exchanges the token instead (<see cref="AddCallCredentials{TProvider}"/>).
    /// </summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder.</returns>
    public static IKaleidoBuilder ForwardCallerToken(this IKaleidoBuilder builder) =>
        builder.AddCallCredentials<ForwardCallerTokenProvider>();

    // One configured client: a process client and a queryable client for the same service; the
    // entry's settings fall back to the shared ones.
    private static void AddClients(IKaleidoBuilder builder, KaleidoClientOptions config, string name, KaleidoClientEntry? entry)
    {
        var baseUrl = !string.IsNullOrWhiteSpace(entry?.BaseUrl)
            ? entry.BaseUrl
            : config.BaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new KaleidoConfigurationException(
                HttpClientErrorCodes.MissingBaseUrl,
                $"Kaleido client '{name}' has no BaseUrl configured. " +
                $"Set 'Kaleido:Clients:{name}:BaseUrl' or the shared 'Kaleido:BaseUrl' fallback.");
        }

        var prefix = entry?.RoutePrefix ?? name.ToLowerInvariant();
        var registryTtl = entry?.RegistryTtl ?? config.RegistryTtl;
        var strictProbe = entry?.StrictRegistryProbe ?? config.StrictRegistryProbe;
        var resilience = config.Resilience.ForClient(builder.Configuration, name);

        builder.AddProcessorClient(o =>
        {
            o.Name = name;
            o.BaseUrl = baseUrl;
            o.RoutePrefix = prefix;
            o.RegistryTtl = registryTtl;
            o.StrictRegistryProbe = strictProbe;
            o.Resilience = resilience;
        });

        builder.AddQueryableClient(o =>
        {
            o.Name = name;
            o.BaseUrl = baseUrl;
            o.RoutePrefix = prefix;
            o.RegistryTtl = registryTtl;
            o.StrictRegistryProbe = strictProbe;
            o.Resilience = resilience;
        });
    }
}
