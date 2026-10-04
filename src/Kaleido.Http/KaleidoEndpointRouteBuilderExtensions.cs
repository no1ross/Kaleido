using Kaleido.Http.Registry;
using Kaleido.Processor.Registry;
using Kaleido.Queryable.Registry;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http;

public static class KaleidoEndpointRouteBuilderExtensions
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
    public static IEndpointConventionBuilder MapKaleidoHttp(
        this IEndpointRouteBuilder endpoints,
        Action<KaleidoHttpMapOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var mapOptions = new KaleidoHttpMapOptions();
        configure?.Invoke(mapOptions);

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

        logger.LogInformation(
                "Kaleido started: {StepCount} process step(s), {ContextCount} query context(s), {ViewCount} query view(s).",
                stepCount,
                contextCount,
                viewCount);

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
            logger.LogWarning(
                "AuthorizationMode is None: Kaleido capability authorization, registry filtering, and process ownership are disabled. " +
                "Set AuthorizationMode to Authenticated or ZeroTrust for enforcement.");
            return;
        }

        var schemes =
            services.GetService<IAuthenticationSchemeProvider>()?
                .GetAllSchemesAsync()
                .GetAwaiter()
                .GetResult();

        if (schemes is null || !schemes.Any())
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
                .Where(c => c.Authorization.IsExplicit() != true)
                .Select(c => c.Name)
                .ToArray();

        if (undeclared.Length > 0)
        {
            logger.LogWarning(
                "ZeroTrust omits {OmittedCount} capability(ies) without an explicit authorization rule: {Capabilities}. " +
                "Declare a service default or a capability rule to publish them.",
                undeclared.Length,
                string.Join(", ", undeclared));
        }
    }

    // Every capability that gets an endpoint: process steps, direct query
    // contexts, and query views (views already carry their effective rule:
    // their own declaration, else their context's).
    private static IReadOnlyList<(string Name, AuthorizationMetadata Authorization)> ExposedCapabilities(
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
            foreach (var context in queryables.Registrations)
            {
                if (context.Kind == QueryContextKind.Direct)
                {
                    capabilities.Add(($"context '{context.Name}'", context.Authorization));
                }

                capabilities.AddRange(
                    context.Views.Select(v =>
                        ($"view '{context.Name}/{v.Name}'", v.Authorization)));
            }
        }

        return capabilities;
    }
}
