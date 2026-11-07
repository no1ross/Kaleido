using Kaleido.Http.Registry;
using Kaleido.Http.Server;
using Kaleido.Http.Server.Registry;
using Kaleido.Platform;
using Kaleido.Platform.Registry;
using Kaleido.Server;
using Kaleido.Server.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Registry.Http;

/// <summary>
/// Maps the control registry's endpoints (#553); the entry point is <see cref="MapKaleidoControlRegistry"/>.
/// </summary>
public static class ControlRegistryEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the control registry: instances register (<c>POST /registry/instances</c>), heartbeat
    /// (<c>PUT /registry/instances/{instanceId}/heartbeat</c>) and deregister
    /// (<c>DELETE /registry/instances/{instanceId}</c>); everyone reads the registry (<c>GET /registry</c>: the
    /// capabilities with their status, with an ETag, 304 and <c>?strict</c> as every Kaleido registry), and other
    /// Kaleido hosts keep their copy fresh (<c>GET /registry/capabilities</c>, <c>GET /registry/availability</c>)
    /// and read each version's status (<c>GET /registry/status</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>GET /registry</c> is unfiltered for a caller satisfying <see cref="KaleidoRegistryOptions.ReaderPolicy"/>
    /// (a Kaleido host, which filters for its own callers), and filtered for any other caller - the UI, a user -
    /// to what it may use, by each capability's published authorization: discovery only, as every service still
    /// enforces its own. The registry host defines the policies the capabilities name (a policy it lacks hides
    /// them). The other reads are for Kaleido hosts only (403 <see cref="RegistryErrorCodes.ReaderNotAllowed"/>
    /// otherwise).
    /// </para>
    /// <para>
    /// Call <c>AddControlRegistry()</c> and choose a registry store first; the registry host maps nothing
    /// else of Kaleido's (it does not call <c>MapKaleidoHttp()</c>).
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <returns>The route group of the registry's endpoints.</returns>
    /// <exception cref="KaleidoConfigurationException">
    /// <c>AddControlRegistry()</c> was not called (<see cref="ConfigurationErrorCodes.InvalidOptions"/>), or no
    /// registry store was chosen (<see cref="ConfigurationErrorCodes.StoresNotChosen"/>).
    /// </exception>
    public static RouteGroupBuilder MapKaleidoControlRegistry(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var services = endpoints.ServiceProvider;
        var options = services.GetService<KaleidoRegistryOptions>()
            ?? throw new KaleidoConfigurationException(ConfigurationErrorCodes.InvalidOptions, "MapKaleidoControlRegistry() requires AddControlRegistry() on the Kaleido builder.");

        if (services.GetService<IServiceProviderIsService>()?.IsService(typeof(IKaleidoRegistryStore)) != true)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.StoresNotChosen,
                "The control registry has no store. Register a durable one (e.g. AddSqliteRegistryStore, or AddRegistryStore<T>()), or choose memory explicitly with AddInMemoryRegistryStore() (development and tests).");
        }

        // The host's registry is the store's view: always "aggregated", kept no longer than a heartbeat.
        services.GetRequiredService<RegistrySnapshot>().Configure(new KaleidoHttpMapOptions
        {
            AggregateRegistry = true,
            RegistryCacheTtl = options.HeartbeatInterval,
            RegistryRefreshCooldown = options.HeartbeatInterval,
        });

        var group = endpoints.MapGroup(ControlRegistryRoutePaths.Group)
            .AddEndpointFilter<KaleidoExceptionEndpointFilter>()
            .AddEndpointFilter<Kaleido.Server.Observability.KaleidoCorrelationEndpointFilter>()
            .AddEndpointFilter<KaleidoJsonEndpointFilter>()
            .AddEndpointFilter<Kaleido.Server.Observability.KaleidoCallerContextEndpointFilter>()
            .WithTags("Control registry");

        MapInstanceEndpoints(group);
        MapReadEndpoints(group);

        return group;
    }

    // Registration, heartbeat and deregistration: an instance of a service, with its service's own token.
    private static void MapInstanceEndpoints(RouteGroupBuilder group)
    {
        group.MapPost(ControlRegistryRoutePaths.Instances, RegisterAsync)
            .WithName(ControlRegistryEndpointNames.Register)
            .WithSummary("Register an instance.")
            .WithDescription(
                "Registers an instance of a service version with the capabilities it publishes, and leases it. " +
                "The caller must be allowed to register the service (Kaleido:Registry:Registrants). " +
                "The answer is the lease: renew it every heartbeat interval it names.")
            .Accepts<RegistryInstanceRegistration>("application/json")
            .Produces<RegistryLease>()
            .ProducesKaleidoErrors(StatusCodes.Status400BadRequest, StatusCodes.Status403Forbidden, StatusCodes.Status415UnsupportedMediaType);

        group.MapPut(ControlRegistryRoutePaths.Heartbeat, HeartbeatAsync)
            .WithName(ControlRegistryEndpointNames.Heartbeat)
            .WithSummary("Renew an instance's lease.")
            .WithDescription(
                "Renews the lease of a registered instance and records its local health. " +
                "404 registry_instance_unknown means the registry does not know the instance (it restarted, failed over or removed it): register again.")
            .Accepts<RegistryHeartbeat>("application/json")
            .Produces<RegistryLease>()
            .ProducesKaleidoErrors(StatusCodes.Status400BadRequest, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status415UnsupportedMediaType);

        group.MapDelete(ControlRegistryRoutePaths.Instance, DeregisterAsync)
            .WithName(ControlRegistryEndpointNames.Deregister)
            .WithSummary("Deregister an instance.")
            .WithDescription("Removes an instance at its shutdown, so it is no longer counted at once.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesKaleidoErrors(StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound);
    }

    // The reads, each with an ETag: the registry for everyone (unfiltered for Kaleido hosts, filtered for any
    // other caller); the documents for keeping a copy fresh, and the status, for Kaleido hosts only
    // (Kaleido:Registry:ReaderPolicy).
    private static void MapReadEndpoints(RouteGroupBuilder group)
    {
        group.MapGet(ControlRegistryRoutePaths.Read, ReadAsync)
            .WithName(ControlRegistryEndpointNames.Read)
            .WithSummary("The registry of every service version.")
            .WithDescription(
                "Returns the registry in one call: every processor and query source of every service version, each with its overall status, and what is missing. " +
                "A Kaleido host (Kaleido:Registry:ReaderPolicy) gets all of it; any other caller only what it may use, by each capability's authorization. " +
                "Send the ETag back as If-None-Match to get 304 while nothing changed; ?strict answers 502 when the registry is partial.")
            .Produces<KaleidoRegistry>()
            .Produces(StatusCodes.Status304NotModified)
            .Produces<KaleidoRegistry>(StatusCodes.Status502BadGateway)
            .ProducesKaleidoErrors();

        group.MapGet(ControlRegistryRoutePaths.Status, StatusAsync)
            .WithName(ControlRegistryEndpointNames.Status)
            .WithSummary("Each service version's status and instances.")
            .WithDescription(
                "Returns each service version's status in detail, its instances included, for operations and Kaleido hosts keeping their copy fresh. " +
                "Its ETag changes with a state, not with a heartbeat alone.")
            .Produces<KaleidoRegistryStatus>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesKaleidoErrors(StatusCodes.Status403Forbidden);

        group.MapGet(ControlRegistryRoutePaths.Availability, AvailabilityAsync)
            .WithName(ControlRegistryEndpointNames.Availability)
            .WithSummary("Each service version's overall availability.")
            .WithDescription(
                "Returns each service version's overall status, current version and address, without its instances, for Kaleido hosts keeping their copy fresh. " +
                "Its ETag changes only when one of those does, not when instances come and go within the same status.")
            .Produces<KaleidoRegistryAvailability>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesKaleidoErrors(StatusCodes.Status403Forbidden);

        group.MapGet(ControlRegistryRoutePaths.Capabilities, CapabilitiesAsync)
            .WithName(ControlRegistryEndpointNames.Capabilities)
            .WithSummary("The capabilities alone.")
            .WithDescription(
                "Returns every processor and query source without their statuses, for Kaleido hosts keeping their copy fresh. " +
                "Its ETag changes only with a deployment.")
            .Produces<KaleidoRegistry>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesKaleidoErrors(StatusCodes.Status403Forbidden);
    }

    // Every write is a service application's (RegistrantPolicy), registering only what it may (ControlRegistry).
    private static async Task<IResult> RegisterAsync(HttpContext httpContext, KaleidoJsonBody<RegistryInstanceRegistration> body, [FromServices] KaleidoRegistryOptions options, [FromServices] IControlRegistry registry, [FromServices] IKaleidoCorrelationContextAccessor caller, CancellationToken cancellationToken) =>
        await SatisfiesAsync(httpContext, options.RegistrantPolicy)
            ? Answer(await registry.RegisterAsync(body.Value, Registrant(httpContext, caller), cancellationToken))
            : Answer(RegistrantNotAllowed);

    private static async Task<IResult> HeartbeatAsync(HttpContext httpContext, Guid instanceId, KaleidoJsonBody<RegistryHeartbeat> body, [FromServices] KaleidoRegistryOptions options, [FromServices] IControlRegistry registry, [FromServices] IKaleidoCorrelationContextAccessor caller, CancellationToken cancellationToken) =>
        await SatisfiesAsync(httpContext, options.RegistrantPolicy)
            ? Answer(await registry.HeartbeatAsync(instanceId, body.Value, Registrant(httpContext, caller), cancellationToken))
            : Answer(RegistrantNotAllowed);

    private static async Task<IResult> DeregisterAsync(HttpContext httpContext, Guid instanceId, [FromServices] KaleidoRegistryOptions options, [FromServices] IControlRegistry registry, [FromServices] IKaleidoCorrelationContextAccessor caller, CancellationToken cancellationToken) =>
        await SatisfiesAsync(httpContext, options.RegistrantPolicy)
            ? Answer(await registry.DeregisterAsync(instanceId, Registrant(httpContext, caller), cancellationToken))
            : Answer(RegistrantNotAllowed);

    private static readonly RegistryAnswer RegistrantNotAllowed =
        RegistryAnswer.Failed(RegistryAnswerKind.NotAllowed, RegistryErrorCodes.RegistrantNotAllowed, "Only service applications register with the control registry (Kaleido:Registry:RegistrantPolicy).");

    // Readers (the reader policy) get the full registry, unfiltered; they filter for their own callers.
    private static async Task<IResult> ReadAsync(HttpContext httpContext, [FromServices] KaleidoRegistryOptions options, CancellationToken cancellationToken)
    {
        // A Kaleido host filters for its own callers; anyone else (the UI, a user) is filtered here, by each
        // capability's published authorization - discovery only: every service still enforces its own.
        var registry = await IsReaderAsync(httpContext, options)
            ? await httpContext.RequestServices.GetRequiredService<IKaleidoUnfilteredRegistry>().GetUnfilteredAsync(cancellationToken)
            : await httpContext.RequestServices.GetRequiredService<IKaleidoRegistryService>().GetForCallerAsync(refresh: false, cancellationToken);

        return httpContext.RegistryResult(registry, options.HeartbeatInterval);
    }

    private static async Task<IResult> StatusAsync(HttpContext httpContext, [FromServices] KaleidoRegistryOptions options, [FromServices] IRegistryStatusReader statuses, CancellationToken cancellationToken)
    {
        if (!await IsReaderAsync(httpContext, options))
        {
            return ReaderNotAllowed();
        }

        var status = await statuses.GetStatusAsync(cancellationToken);

        return Revisioned(httpContext, status.Revision, status);
    }

    // Each version's overall availability, for the hosts keeping their copy fresh: no instances.
    private static async Task<IResult> AvailabilityAsync(HttpContext httpContext, [FromServices] KaleidoRegistryOptions options, [FromServices] IKaleidoRegistrySource registry, CancellationToken cancellationToken)
    {
        if (!await IsReaderAsync(httpContext, options))
        {
            return ReaderNotAllowed();
        }

        var availability = await registry.GetAvailabilityAsync(cancellationToken);

        return Revisioned(httpContext, availability.Revision, availability);
    }

    // The capabilities alone, for the hosts keeping their copy fresh: their revision changes only with a deployment.
    private static async Task<IResult> CapabilitiesAsync(HttpContext httpContext, [FromServices] KaleidoRegistryOptions options, [FromServices] IRegistryStatusReader registry, CancellationToken cancellationToken)
    {
        if (!await IsReaderAsync(httpContext, options))
        {
            return ReaderNotAllowed();
        }

        var capabilities = await registry.GetCapabilitiesAsync(cancellationToken);

        return Revisioned(httpContext, capabilities.Revision, capabilities);
    }

    // The ETag of what is sent, and 304 to a reader that already has it.
    private static IResult Revisioned<T>(HttpContext httpContext, string? revision, T value)
    {
        var etag = $"\"{revision}\"";
        httpContext.Response.Headers.ETag = etag;
        httpContext.Response.Headers.CacheControl = "private, no-cache";

        return httpContext.Request.Headers.IfNoneMatch.Any(x => string.Equals(x, etag, StringComparison.Ordinal))
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Ok(value);
    }

    private static Task<bool> IsReaderAsync(HttpContext httpContext, KaleidoRegistryOptions options) =>
        SatisfiesAsync(httpContext, options.ReaderPolicy);

    // The caller satisfies the host's policy. Not enforced (AuthorizationMode None, development): anyone does.
    private static async Task<bool> SatisfiesAsync(HttpContext httpContext, string? policy)
    {
        var services = httpContext.RequestServices;

        return !services.GetRequiredService<IKaleidoAuthorizer>().IsEnforced
            || (policy is not null
                && (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(httpContext.User, policy)).Succeeded);
    }

    private static IResult ReaderNotAllowed() =>
        Results.Json(
            new KaleidoErrorResponse([new KaleidoError(RegistryErrorCodes.ReaderNotAllowed, "Only Kaleido hosts read this (Kaleido:Registry:ReaderPolicy); read GET /registry instead, filtered for you.")]),
            KaleidoJsonOptions.Options,
            statusCode: StatusCodes.Status403Forbidden);

    // The registering service as its token describes it: its identity (the actor - act/azp/appid - else its
    // subject) and its roles, read from the principal as IsInRole reads them: each identity's own
    // RoleClaimType (#433, #574).
    private static RegistryCaller Registrant(HttpContext httpContext, IKaleidoCorrelationContextAccessor caller) =>
        new(
            caller.Current.CallerActor ?? caller.Current.CallerId,
            [.. httpContext.User.Identities
                .Where(identity => identity.IsAuthenticated)
                .SelectMany(identity => identity.FindAll(identity.RoleClaimType))
                .Select(claim => claim.Value)
                .Distinct(StringComparer.Ordinal)]);

    private static IResult Answer(RegistryAnswer answer) =>
        answer.Kind switch
        {
            RegistryAnswerKind.Leased => Results.Ok(answer.Lease),
            RegistryAnswerKind.Removed => Results.NoContent(),
            _ => Results.Json(
                new KaleidoErrorResponse([new KaleidoError(answer.Code ?? RegistryErrorCodes.InvalidRegistration, answer.Message ?? string.Empty)]),
                KaleidoJsonOptions.Options,
                statusCode: answer.Kind switch
                {
                    RegistryAnswerKind.NotAllowed => StatusCodes.Status403Forbidden,
                    RegistryAnswerKind.Unknown => StatusCodes.Status404NotFound,
                    _ => StatusCodes.Status400BadRequest,
                }),
        };
}
