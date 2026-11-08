using Kaleido.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client.Registration;

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

        if (config.Clients.Count > 0)
        {
            // Every Kaleido client ends with the host's Kaleido client handlers, then the on-behalf-of guard (#574).
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, KaleidoClientHandlerFilter>());

            // At start: warn about a client that would send a caller's credential over a non-HTTPS,
            // non-loopback connection (#434).
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, KaleidoClientTransportCheck>());
        }

        return builder;
    }

    /// <summary>
    /// Adds <typeparamref name="THandler"/>, a standard <see cref="DelegatingHandler"/>, to every Kaleido client
    /// (the configured ones and the control registry's), and to no other <c>HttpClient</c> of the host (#574).
    /// It is where the host sets the calls' credentials, with any library: a client-credentials token on the
    /// service's own calls, and, with <c>Kaleido:OnBehalfOfCalls = Exchange</c>, the caller's exchanged token on
    /// the calls made for a caller. <see cref="KaleidoCallContext.Of"/> tells it which call it is.
    /// </summary>
    /// <remarks>
    /// Handlers run in the order they are added, after the handlers configured on the client itself; Kaleido's
    /// own guard runs last and enforces <see cref="KaleidoClientOptions.OnBehalfOfCalls"/>: whatever a handler sets,
    /// an anonymous caller's call carries nothing, a <see cref="KaleidoOnBehalfOfCalls.Forward"/> call carries the
    /// caller's token, and an <see cref="KaleidoOnBehalfOfCalls.Exchange"/> call never carries it unchanged.
    /// The handler is created per client pipeline (transient), like any <c>AddHttpMessageHandler</c> handler:
    /// keep tokens in a cache it depends on, not in its own fields.
    /// </remarks>
    /// <typeparam name="THandler">The host's handler.</typeparam>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder.</returns>
    public static IKaleidoBuilder AddKaleidoClientHandler<THandler>(this IKaleidoBuilder builder)
        where THandler : DelegatingHandler
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddTransient<THandler>();

        // One registration per handler type, in the order they are added (a handler added twice runs once).
        if (!builder.Services.Any(x => x.ImplementationInstance is KaleidoClientHandlerRegistration known && known.HandlerType == typeof(THandler)))
        {
            builder.Services.AddSingleton(new KaleidoClientHandlerRegistration(typeof(THandler)));
        }

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, KaleidoClientHandlerFilter>());
        return builder;
    }

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
            Resilience = config.Resilience.ForClient(builder.Configuration, name),
        };

        builder.AddKaleidoClient(client);
    }
}
