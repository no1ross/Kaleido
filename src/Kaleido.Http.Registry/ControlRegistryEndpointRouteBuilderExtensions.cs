using Kaleido.Http.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Registry;

/// <summary>
/// Maps the control registry's endpoints (#553); the entry point is <see cref="MapKaleidoControlRegistry"/>.
/// </summary>
public static class ControlRegistryEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the control registry: instances register (<c>POST /registry/instances</c>), heartbeat
    /// (<c>PUT /registry/instances/{instanceId}/heartbeat</c>) and deregister
    /// (<c>DELETE /registry/instances/{instanceId}</c>); other Kaleido hosts read the full registry
    /// (<c>GET /registry</c>: the capabilities, with an ETag, 304 and <c>?strict</c> as every Kaleido registry)
    /// and each service version's status (<c>GET /registry/status</c>, with its own ETag).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both reads are for services only: a caller must satisfy <see cref="KaleidoRegistryOptions.ReaderPolicy"/>
    /// (403 <see cref="RegistryErrorCodes.ReaderNotAllowed"/> otherwise), and gets everything, unfiltered. Users
    /// read the registry a router or orchestrator exposes, filtered for them. The statuses are apart from the
    /// capabilities because they change whenever an instance starts or stops; the capabilities only with a
    /// deployment.
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
            .AddEndpointFilter<Observability.KaleidoCorrelationEndpointFilter>()
            .AddEndpointFilter<KaleidoJsonEndpointFilter>()
            .AddEndpointFilter<Observability.KaleidoCallerContextEndpointFilter>()
            .WithTags("Control registry");

        group.MapPost(ControlRegistryRoutePaths.Instances, RegisterAsync).WithName(ControlRegistryEndpointNames.Register).WithSummary("Register an instance.");
        group.MapPut(ControlRegistryRoutePaths.Heartbeat, HeartbeatAsync).WithName(ControlRegistryEndpointNames.Heartbeat).WithSummary("Renew an instance's lease.");
        group.MapDelete(ControlRegistryRoutePaths.Instance, DeregisterAsync).WithName(ControlRegistryEndpointNames.Deregister).WithSummary("Deregister an instance.");
        group.MapGet(ControlRegistryRoutePaths.Read, ReadAsync).WithName(ControlRegistryEndpointNames.Read).WithSummary("The full registry of every service version, for other Kaleido hosts.");
        group.MapGet(ControlRegistryRoutePaths.Status, StatusAsync).WithName(ControlRegistryEndpointNames.Status).WithSummary("Each service version's status, for other Kaleido hosts.");

        return group;
    }

    private static async Task<IResult> RegisterAsync(KaleidoJsonBody<RegistryInstanceRegistration> body, [FromServices] IControlRegistry registry, [FromServices] IKaleidoCorrelationContextAccessor caller, CancellationToken cancellationToken) =>
        Answer(await registry.RegisterAsync(body.Value, Registrant(caller), cancellationToken));

    private static async Task<IResult> HeartbeatAsync(Guid instanceId, KaleidoJsonBody<RegistryHeartbeat> body, [FromServices] IControlRegistry registry, [FromServices] IKaleidoCorrelationContextAccessor caller, CancellationToken cancellationToken) =>
        Answer(await registry.HeartbeatAsync(instanceId, body.Value, Registrant(caller), cancellationToken));

    private static async Task<IResult> DeregisterAsync(Guid instanceId, [FromServices] IControlRegistry registry, [FromServices] IKaleidoCorrelationContextAccessor caller, CancellationToken cancellationToken) =>
        Answer(await registry.DeregisterAsync(instanceId, Registrant(caller), cancellationToken));

    // Readers (the reader policy) get the full registry, unfiltered; they filter for their own callers.
    private static async Task<IResult> ReadAsync(HttpContext httpContext, [FromServices] KaleidoRegistryOptions options, CancellationToken cancellationToken)
    {
        if (!await IsReaderAsync(httpContext, options))
        {
            return ReaderNotAllowed();
        }

        var registry = await httpContext.RequestServices.GetRequiredService<IKaleidoUnfilteredRegistry>().GetUnfilteredAsync(cancellationToken);

        return httpContext.RegistryResult(registry, options.HeartbeatInterval);
    }

    private static async Task<IResult> StatusAsync(HttpContext httpContext, [FromServices] KaleidoRegistryOptions options, [FromServices] IKaleidoRegistrySource statuses, CancellationToken cancellationToken)
    {
        if (!await IsReaderAsync(httpContext, options))
        {
            return ReaderNotAllowed();
        }

        var status = await statuses.GetStatusAsync(cancellationToken);
        var etag = $"\"{status.Revision}\"";
        httpContext.Response.Headers.ETag = etag;
        httpContext.Response.Headers.CacheControl = "private, no-cache";

        return httpContext.Request.Headers.IfNoneMatch.Any(x => string.Equals(x, etag, StringComparison.Ordinal))
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Ok(status);
    }

    // Not enforced (AuthorizationMode None, development): anyone reads.
    private static async Task<bool> IsReaderAsync(HttpContext httpContext, KaleidoRegistryOptions options)
    {
        var services = httpContext.RequestServices;

        return !services.GetRequiredService<IKaleidoAuthorizer>().IsEnforced
            || (options.ReaderPolicy is { } policy
                && (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(httpContext.User, policy)).Succeeded);
    }

    private static IResult ReaderNotAllowed() =>
        Results.Json(
            new KaleidoErrorResponse([new KaleidoError(RegistryErrorCodes.ReaderNotAllowed, "Only Kaleido hosts read the control registry (Kaleido:Registry:ReaderPolicy); read a router's or orchestrator's own registry instead.")]),
            KaleidoJsonOptions.Options,
            statusCode: StatusCodes.Status403Forbidden);

    // The registering service's identity: the actor of its token (act/azp/appid), else its subject.
    private static string? Registrant(IKaleidoCorrelationContextAccessor caller) =>
        caller.Current.CallerActor ?? caller.Current.CallerId;

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
