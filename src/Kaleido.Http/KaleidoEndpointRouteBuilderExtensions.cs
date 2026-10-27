using Kaleido.Http.Registry;
using Kaleido.Processor.Registry;
using Kaleido.Queryable.Registry;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http;

/// <summary>
/// Maps the Kaleido HTTP endpoints registered by <c>AddHttp()</c>: Process, Queryable and the
/// unified registry. The entry point is <see cref="MapKaleidoHttp"/>, which also runs the startup
/// authorization checks.
/// </summary>
public static partial class KaleidoEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps all registered Kaleido endpoints in one call.
    /// The framework auto-detects which runtimes are active (Process and/or Queryable)
    /// and maps only those, plus the unified <c>GET /{service}/registry</c> endpoint.
    /// Set <see cref="KaleidoHttpMapOptions.AggregateRegistry"/> to also fan out to
    /// downstream services registered via <c>AddHttpClients()</c> (routers/gateways).
    /// Returns an <see cref="IEndpointConventionBuilder"/> that
    /// propagates conventions (e.g. <c>.RequireAuthorization()</c>) to all mapped endpoints.
    /// </summary>
    /// <remarks>
    /// The <c>Kaleido:Http:Map</c> configuration section (from the host's <see cref="IConfiguration"/>) is
    /// laid over a copy of <paramref name="options"/>: configuration overrides code, setting by setting
    /// (ADR 0030). The result is validated; an invalid setting throws <c>invalid_http_options</c>.
    /// </remarks>
    /// <param name="endpoints">The host's endpoint route builder.</param>
    /// <param name="options">The options set in code; <see langword="null"/> uses the defaults.</param>
    /// <returns>A convention builder for every mapped Kaleido endpoint.</returns>
    public static IEndpointConventionBuilder MapKaleidoHttp(
        this IEndpointRouteBuilder endpoints,
        KaleidoHttpMapOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var mapOptions = ResolveMapOptions(endpoints.ServiceProvider, options ?? new KaleidoHttpMapOptions());

        var logger = endpoints.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Kaleido.Startup");

        ValidateAuthorization(endpoints.ServiceProvider, logger);

        var hasProcess = endpoints.ServiceProvider.GetService<IProcessorStepRegistry>() is not null;
        var hasQueryable = endpoints.ServiceProvider.GetService<IQueryableRegistry>() is not null;

        var stepCount = hasProcess
            ? endpoints.ServiceProvider.GetRequiredService<IProcessorStepRegistry>().Registrations.Count
            : 0;

        var contextCount = 0;
        var viewCount = 0;

        if (hasQueryable)
        {
            var registrations = endpoints.ServiceProvider
                .GetRequiredService<IQueryableRegistry>()
                .Registrations;

            contextCount = registrations.Count;
            viewCount = registrations.Sum(c => c.Views.Count);
        }

        LogStarted(logger, stepCount, contextCount, viewCount);

        var builders = new List<IEndpointConventionBuilder>();

        if (hasProcess)
        {
            builders.Add(
                endpoints.MapProcessor());
        }

        if (hasQueryable)
        {
            builders.Add(
                endpoints.MapQueryable());
        }

        // Every Kaleido host exposes its registry; aggregation mode fans out to
        // downstream clients (validated inside MapRegistry).
        if (hasProcess || hasQueryable || mapOptions.AggregateRegistry)
        {
            builders.Add(
                endpoints.MapRegistry(mapOptions));
        }

        return new RouteHandlerBuilder(builders);
    }

    // The host's Kaleido:Http:Map section over the options from code (ADR 0030), validated.
    private static KaleidoHttpMapOptions ResolveMapOptions(IServiceProvider services, KaleidoHttpMapOptions options)
    {
        var resolved = services.GetService<IConfiguration>() is { } configuration
            ? configuration.GetSection(KaleidoHttpMapOptions.SectionName).BindOver(options, ConfigurationErrorCodes.InvalidHttpOptions)
            : options;

        KaleidoHttpMapOptions.Validate(resolved);

        return resolved;
    }

    // Startup authorization checks, by mode:
    // - None: warn once that capability authorization is disabled.
    // - Authenticated / ZeroTrust: fail if no authentication scheme is registered
    //   (every request would otherwise fail at runtime with no scheme to challenge).
    // - ZeroTrust: omit capabilities without an explicit authorization rule.
    private static void ValidateAuthorization(
        IServiceProvider services,
        ILogger logger)
    {
        var mode = services.GetRequiredService<KaleidoServiceOptions>().AuthorizationMode;
        var capabilities = ExposedCapabilities(services);

        if (mode == KaleidoAuthorizationMode.None)
        {
            LogAuthorizationDisabled(logger);
            return;
        }

        var schemes =
            services.GetService<IAuthenticationSchemeProvider>()?
                .GetAllSchemesAsync()
                .GetAwaiter()
                .GetResult();

        if (schemes?.Any() != true)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.AuthenticationNotConfigured,
                $"AuthorizationMode is {mode} but no authentication scheme is registered. " +
                "Call AddAuthentication(...) with at least one scheme, plus UseAuthentication()/UseAuthorization(), " +
                "or set AuthorizationMode = None.");
        }

        if (mode != KaleidoAuthorizationMode.ZeroTrust)
        {
            return;
        }

        var undeclared =
            capabilities
                .Where(c => !c.Authorization.IsExplicit())
                .Select(c => c.Name)
                .ToArray();

        if (undeclared.Length > 0)
        {
            LogZeroTrustOmits(logger, undeclared.Length, string.Join(", ", undeclared));
        }
    }

    // Every capability that gets an endpoint: process steps, query sources,
    // and query views (views already carry their effective rule: their own
    // declaration, else their source's).
    private static List<(string Name, AuthorizationMetadata Authorization)> ExposedCapabilities(
        IServiceProvider services)
    {
        var capabilities = new List<(string Name, AuthorizationMetadata Authorization)>();

        if (services.GetService<IProcessorStepRegistry>() is { } steps)
        {
            capabilities.AddRange(
                steps.Registrations.Select(s =>
                    ($"step '{s.Metadata.Name}'", s.Metadata.Authorization)));
        }

        if (services.GetService<IQueryableRegistry>() is { } queryables)
        {
            foreach (var source in queryables.Registrations)
            {
                capabilities.Add(($"source '{source.Name}'", source.Authorization));

                capabilities.AddRange(
                    source.Views.Select(v =>
                        ($"view '{source.Name}/{v.Name}'", v.Authorization)));
            }
        }

        return capabilities;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Kaleido started: {StepCount} process step(s), {ContextCount} query context(s), {ViewCount} query view(s).")]
    private static partial void LogStarted(ILogger logger, int stepCount, int contextCount, int viewCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AuthorizationMode is None: Kaleido capability authorization, registry filtering, and process ownership are disabled. Set AuthorizationMode to Authenticated or ZeroTrust for enforcement.")]
    private static partial void LogAuthorizationDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ZeroTrust omits {OmittedCount} capability(ies) without an explicit authorization rule: {Capabilities}. Declare a service default or a capability rule to publish them.")]
    private static partial void LogZeroTrustOmits(ILogger logger, int omittedCount, string capabilities);
}
