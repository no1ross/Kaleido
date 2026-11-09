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
    /// Registers one gateway HTTP client at <c>Kaleido:BaseUrl</c> for Process and Queryable calls.
    /// Client factories resolve published service/version routes from the hydrated central registry.
    /// </summary>
    public static IKaleidoBuilder AddHttpClients(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Configuration.GetSection($"{KaleidoServiceOptions.SectionName}:Clients").GetChildren().Any())
        {
            throw new KaleidoConfigurationException(HttpClientErrorCodes.InvalidClientOptions,
                "Kaleido:Clients is no longer a list of downstream endpoints. Configure only Kaleido:BaseUrl with the gateway address.");
        }

        var config = builder.Configuration
            .GetSection(KaleidoServiceOptions.SectionName)
            .BindOver(new KaleidoClientOptions(), HttpClientErrorCodes.InvalidClientOptions);
        KaleidoClientOptions.Validate(config);
        if (string.IsNullOrWhiteSpace(config.BaseUrl))
        {
            throw new KaleidoConfigurationException(HttpClientErrorCodes.MissingBaseUrl,
                "Kaleido:BaseUrl must name the gateway before AddHttpClients() registers the HTTP client.");
        }

        if (!Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out var gateway) || gateway.Scheme is not ("http" or "https") || gateway.UserInfo.Length > 0)
        {
            throw new KaleidoConfigurationException(HttpClientErrorCodes.InvalidClientOptions,
                "Kaleido:BaseUrl must be the gateway's absolute http or https URL without embedded credentials.");
        }

        var services = builder.Services;
        if (services.Any(x => x.ServiceType == typeof(KaleidoClientOptions)))
        {
            return builder;
        }

        services.AddSingleton(config);
        RegisterGateway(services, gateway, config);
        return builder;
    }

    /// <summary>
    /// Adds <typeparamref name="THandler"/>, a standard <see cref="DelegatingHandler"/>, to every Kaleido client
    /// (the one gateway client and the control registry's), and to no other <c>HttpClient</c> of the host (#574).
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

    private static void RegisterGateway(IServiceCollection services, Uri gateway, KaleidoClientOptions config)
    {
        var httpClient = services.AddHttpClient(KaleidoClientTransport.ClientName, client =>
        {
            client.BaseAddress = new Uri(gateway.ToString().TrimEnd('/') + "/");
            client.Timeout = config.Resilience.Enabled ? Timeout.InfiniteTimeSpan : config.Resilience.TotalTimeout;
        });
        if (config.Resilience.Enabled)
        {
            httpClient.AddResilienceHandler("kaleido", pipeline => pipeline.AddKaleidoResilience(config.Resilience));
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IKaleidoInboundCallerAccessor, KaleidoInboundCallerAccessor>();
        services.TryAddScoped<IKaleidoProcessForwarding, KaleidoProcessForwarding>();
        services.TryAddScoped<ICorrelationHeaderStamper, CorrelationHeaderStamper>();
        services.TryAddScoped<IKaleidoClientTransport, KaleidoClientTransport>();
        services.TryAddScoped<IKaleidoProcessorClientFactory, KaleidoProcessorClientFactory>();
        services.TryAddScoped<IKaleidoQueryableClientFactory, KaleidoQueryableClientFactory>();
        services.TryAddSingleton<IKaleidoTransportSelector, KaleidoTransportSelector>();
        services.TryAddScoped<IKaleidoRegistryTargetResolver, KaleidoRegistryTargetResolver>();
        services.AddKeyedScoped<IKaleidoProcessorClient>(KeyedService.AnyKey,
            (provider, key) => provider.GetRequiredService<IKaleidoProcessorClientFactory>().GetClient(ServiceName(key)));
        services.AddKeyedScoped<IKaleidoQueryableClient>(KeyedService.AnyKey,
            (provider, key) => provider.GetRequiredService<IKaleidoQueryableClientFactory>().GetClient(ServiceName(key)));
        services.TryAddKeyedTransient<KaleidoOnBehalfOfGuard>(KaleidoClientTransport.ClientName);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, KaleidoClientHandlerFilter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, KaleidoClientTransportCheck>());
    }

    private static string ServiceName(object? key) =>
        key is string name && !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new KaleidoConfigurationException(HttpClientErrorCodes.UnknownClient, "A keyed Kaleido client requires a service name.");
}
