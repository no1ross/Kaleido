using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido;

/// <summary>
/// Registers the Kaleido foundation on an <see cref="IServiceCollection"/> with <see cref="AddKaleido"/>.
/// Capabilities, Eventing, providers and transports extend the returned <see cref="IKaleidoBuilder"/>.
/// </summary>
public static class KaleidoServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Kaleido foundation. Process and Queryable capabilities are registered separately
    /// with <c>AddProcess()</c> and <c>AddQueryable()</c>.
    /// </summary>
    /// <remarks>
    /// The <c>Kaleido</c> configuration section is laid over a copy of <paramref name="options"/>:
    /// a setting configuration names replaces the value from code, every other setting keeps it, and
    /// <paramref name="options"/> itself is not changed (ADR 0030). The result is validated - nothing is
    /// trimmed or substituted, an invalid value stops startup - and registered as the
    /// <see cref="KaleidoServiceOptions"/> singleton. It registers logging, scoped correlation
    /// access and shared metadata primitives. Hosting composes Process, Queryable and Eventing;
    /// a provider registers any stores those capabilities need.
    /// Transports and providers are added on that builder (e.g. <c>AddHttp</c>).
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">
    /// The application configuration; the <c>Kaleido</c> section overrides <paramref name="options"/>, and
    /// the configuration flows to every subsystem through <see cref="IKaleidoBuilder.Configuration"/>.
    /// </param>
    /// <param name="options">
    /// The options set in code, e.g. <c>ServiceName</c> and <c>Assemblies</c> (which only code can set);
    /// settings left out keep their defaults. <see langword="null"/> uses the defaults.
    /// </param>
    /// <returns>The builder for chaining further Kaleido registrations.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KaleidoConfigurationException">
    /// The options are invalid: e.g. a missing or invalid <see cref="KaleidoServiceOptions.ServiceName"/>,
    /// or no <see cref="KaleidoServiceOptions.Assemblies"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// builder.Services.AddKaleido(builder.Configuration, new()
    /// {
    ///     ServiceName = "intake",
    ///     Assemblies = [typeof(Program).Assembly],
    /// });
    /// </code>
    /// </example>
    public static IKaleidoBuilder AddKaleido(
        this IServiceCollection services,
        IConfiguration configuration,
        KaleidoServiceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // One Kaleido service per host: a second call would silently replace the first one's options
        // (its service name and assemblies), so it is refused (#301).
        if (services.Any(x => x.ServiceType == typeof(KaleidoServiceOptions)))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.DuplicateRegistration,
                "AddKaleido() was called more than once on this service collection. A host runs one Kaleido service: configure it in a single AddKaleido() call.");
        }

        // Configuration over code, on a copy (ADR 0030); validated here, at composition, rather than at
        // first resolution. The registered options hold their own copy of the assemblies.
        var serviceOptions = configuration.BindOptions(options);
        serviceOptions = serviceOptions with
        {
            Assemblies = Array.AsReadOnly([.. serviceOptions.Assemblies ?? []]),
        };
        services.AddSingleton(serviceOptions);

        // TryAdd-based: registers ILoggerFactory/ILogger<T> when the host hasn't —
        // provider configuration via ILoggingBuilder still applies normally.
        services.AddLogging();

        services.AddScoped<KaleidoCorrelationContextAccessor>();
        services.TryAddScoped<IKaleidoCorrelationContextAccessor>(
            sp => sp.GetRequiredService<KaleidoCorrelationContextAccessor>());
        services.TryAddScoped<IKaleidoCorrelationContextInitializer>(
            sp => sp.GetRequiredService<KaleidoCorrelationContextAccessor>());
        services.TryAddScoped<IKaleidoInboundCallerAccessor, KaleidoInboundCallerAccessor>(); // ADR 0022

        // The clock of every lease, expiry, retry, TTL and circuit decision (#460); a host (or a
        // test) that registers its own TimeProvider first keeps it.
        services.TryAddSingleton(TimeProvider.System);

        services.TryAddSingleton<TypeDescriber>();
        services.TryAddSingleton<ConstraintMapper>();

        var builder = new KaleidoBuilder(services, configuration, serviceOptions);

        return builder;
    }

    // Replaces the default; the same implementation again is a no-op, a different one a conflict.
    internal static IKaleidoBuilder Replace<TService, TImplementation, TDefault>(
        this IKaleidoBuilder builder,
        string what,
        string code = ConfigurationErrorCodes.ConflictingStore)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(builder);

        var registered = RegisteredType<TService>(builder.Services);

        if (registered == typeof(TImplementation))
        {
            return builder;
        }

        if (registered is not null && registered != typeof(TDefault))
        {
            throw new KaleidoConfigurationException(
                code,
                $"Two {what} implementations were registered: {registered.Name} and {typeof(TImplementation).Name}. A service has one {what}.");
        }

        builder.Services.RemoveAll<TService>();
        builder.Services.AddScoped<TService, TImplementation>();

        return builder;
    }

    // The implementation registered for a service: its type, an instance's type, or - for a factory,
    // whose type is unknown until resolved - the service type itself (never a default).
    private static Type? RegisteredType<TService>(IServiceCollection services) =>
        services.LastOrDefault(x => x.ServiceType == typeof(TService)) is { } registration
            ? registration.ImplementationType ?? registration.ImplementationInstance?.GetType() ?? typeof(TService)
            : null;
}
