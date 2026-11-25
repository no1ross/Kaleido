using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client.Registration;

/// <summary>Shared gateway HTTP client configuration and credential-handler pipeline.</summary>
public static class KaleidoHttpClientPipelineServiceCollectionExtensions
{
    /// <summary>Configures the gateway, resilience and caller-credential policy for installed Kaleido HTTP clients.</summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <param name="options">Code settings overridden by the <c>Kaleido</c> section.</param>
    /// <returns>The same builder.</returns>
    public static IKaleidoBuilder AddKaleidoHttpClientPipeline(this IKaleidoBuilder builder, KaleidoClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Configuration.GetSection("Kaleido:Clients").GetChildren().Any())
        {
            throw new KaleidoConfigurationException(HttpClientErrorCodes.InvalidClientOptions,
                "Kaleido:Clients no longer configures per-service endpoints; use only Kaleido:BaseUrl for the gateway.");
        }

        var services = builder.Services;
        if (services.Any(x => x.ServiceType == typeof(KaleidoClientOptions)))
        {
            return builder;
        }

        var configured = builder.BindOptions(options).Snapshot();
        if (string.IsNullOrWhiteSpace(configured.BaseUrl))
        {
            throw new KaleidoConfigurationException(HttpClientErrorCodes.MissingBaseUrl, "Kaleido:BaseUrl must name the gateway before a Kaleido HTTP client is registered.");
        }

        if (!Uri.TryCreate(configured.BaseUrl, UriKind.Absolute, out var gateway)
            || gateway.Scheme is not ("http" or "https") || gateway.UserInfo.Length > 0)
        {
            throw new KaleidoConfigurationException(HttpClientErrorCodes.InvalidClientOptions, "Kaleido:BaseUrl must be an absolute http or https gateway URL without embedded credentials.");
        }

        services.AddSingleton(configured);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, KaleidoClientTransportCheck>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, KaleidoClientHandlerFilter>());
        return builder;
    }

    /// <summary>Adds a host credential handler to Kaleido's named HTTP clients, never unrelated clients.</summary>
    /// <typeparam name="THandler">The host's transient credential handler.</typeparam>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder.</returns>
    public static IKaleidoBuilder AddKaleidoClientHandler<THandler>(this IKaleidoBuilder builder) where THandler : DelegatingHandler
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddTransient<THandler>();
        if (!builder.Services.Any(x => x.ImplementationInstance is KaleidoClientHandlerRegistration existing && existing.HandlerType == typeof(THandler)))
        {
            builder.Services.AddSingleton(new KaleidoClientHandlerRegistration(typeof(THandler)));
        }

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, KaleidoClientHandlerFilter>());
        return builder;
    }

    internal static IHttpClientBuilder AddKaleidoNamedClient(this IKaleidoBuilder builder, string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var settings = builder.Services.LastOrDefault(x => x.ServiceType == typeof(KaleidoClientOptions))?.ImplementationInstance as KaleidoClientOptions
            ?? throw new KaleidoConfigurationException(HttpClientErrorCodes.InvalidClientOptions, "Call AddKaleidoHttpClientPipeline() before registering a Kaleido HTTP client.");
        var gateway = new Uri(settings.BaseUrl?.TrimEnd('/') + "/", UriKind.Absolute);
        var client = builder.Services.AddHttpClient(name, http =>
        {
            http.BaseAddress = gateway;
            http.Timeout = settings.Resilience.Enabled ? Timeout.InfiniteTimeSpan : settings.Resilience.TotalTimeout;
        });
        builder.Services.TryAddTransient<KaleidoClientCallLoggingHandler>();
        client.AddHttpMessageHandler<KaleidoClientCallLoggingHandler>();
        if (settings.Resilience.Enabled)
        {
            client.AddResilienceHandler("kaleido", pipeline => pipeline.AddSafeResilience(settings.Resilience));
        }

        builder.Services.TryAddKeyedTransient<KaleidoOnBehalfOfGuard>(name);
        if (!builder.Services.Any(x => x.ImplementationInstance is KaleidoHttpClientName registered && string.Equals(registered.Name, name, StringComparison.Ordinal)))
        {
            builder.Services.AddSingleton(new KaleidoHttpClientName(name));
        }

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, KaleidoClientHandlerFilter>());
        return client;
    }

    internal static string RequireServiceName(this object? key) =>
        key is string name && !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new KaleidoConfigurationException(HttpClientErrorCodes.UnknownClient, "A keyed Kaleido client requires a service name.");
}
