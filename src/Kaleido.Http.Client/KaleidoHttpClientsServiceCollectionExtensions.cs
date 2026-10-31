using Kaleido.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

        var config = builder.Configuration
            .GetSection(KaleidoServiceOptions.SectionName)
            .BindOver(new KaleidoClientOptions(), HttpClientErrorCodes.InvalidClientOptions);
        KaleidoClientOptions.Validate(config);
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

            // At start (a provider may be registered after this call): warn about a client that would
            // send a caller's credential over a non-HTTPS, non-loopback connection (#434).
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, KaleidoClientTransportCheck>());
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
    /// <remarks>
    /// The token is passed through unchanged. For an OAuth-protected MCP endpoint, the MCP authorization
    /// specification forbids passing the token issued for the MCP server through to downstream APIs:
    /// there, use <see cref="AddCallCredentials{TProvider}"/> with a provider that obtains a token for
    /// each service (for example OAuth on-behalf-of). Configure the clients with <c>https</c> base URLs:
    /// a client with a non-HTTPS, non-loopback base URL is reported with a warning at startup, since the
    /// caller's credential would cross the network in clear text (ADR 0027).
    /// </remarks>
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

        // A relative or malformed URL (or another scheme) is refused here, naming the client and the key,
        // instead of a raw UriFormatException when the HTTP client is built (#521).
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var address) ||
            address.Scheme is not ("http" or "https"))
        {
            var key = !string.IsNullOrWhiteSpace(entry?.BaseUrl) ? $"Kaleido:Clients:{name}:BaseUrl" : "Kaleido:BaseUrl";

            throw new KaleidoConfigurationException(
                HttpClientErrorCodes.InvalidClientOptions,
                $"Kaleido client '{name}' has BaseUrl '{baseUrl}' ({key}), which is not an absolute http or https URL, e.g. 'https://radiology:8080'.");
        }

        var prefix = entry?.RoutePrefix ?? name.ToLowerInvariant();

        // The prefix is the downstream service's ServiceName in every route; anything else can only
        // fail at call time (and a '..' or '/' would leave the prefix), so refuse it at startup.
        if (!KaleidoServiceOptions.IsValidServiceName(prefix))
        {
            throw new KaleidoConfigurationException(
                HttpClientErrorCodes.InvalidRoutePrefix,
                $"Kaleido client '{name}' has route prefix '{prefix}', which is not a service name: it must equal the " +
                "downstream service's ServiceName ([a-z0-9][a-z0-9_-]*). " +
                $"Set 'Kaleido:Clients:{name}:RoutePrefix' to that service name.");
        }

        var client = new KaleidoHttpClientOptions
        {
            Name = name,
            BaseUrl = baseUrl,
            RoutePrefix = prefix,
            RegistryTtl = entry?.RegistryTtl ?? config.RegistryTtl,
            StrictRegistryProbe = entry?.StrictRegistryProbe ?? config.StrictRegistryProbe,
            Resilience = config.Resilience.ForClient(builder.Configuration, name),
        };

        builder.AddKaleidoClient(client);
    }
}
