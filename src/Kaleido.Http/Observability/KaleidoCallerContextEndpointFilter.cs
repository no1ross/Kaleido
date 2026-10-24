using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Observability;

/// <summary>
/// Completes the caller fields on the ambient
/// <see cref="KaleidoCorrelationContext"/> once the host's authentication
/// middleware has populated <c>HttpContext.User</c>. Runs inside endpoint
/// execution — always after auth middleware — so caller identity is derived
/// from the authenticated principal, never from headers. Positional ordering
/// makes registration order irrelevant.
/// </summary>
internal sealed class KaleidoCallerContextEndpointFilter
    : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        context.HttpContext.InitializeKaleidoCaller();

        return await next(context);
    }
}

internal static class KaleidoCallerContextHttpContextExtensions
{
    private const string SubjectClaimType = "sub";

    /// <summary>
    /// Sets the caller (name, subject id and roles) on the request's <see cref="KaleidoCorrelationContext"/>
    /// from the authenticated <c>HttpContext.User</c>. Kaleido's endpoints do this through
    /// <see cref="KaleidoCallerContextEndpointFilter"/>; in-host consumers reached through other
    /// endpoints (e.g. the registry service called by the MCP adapter) do it before authorizing.
    /// </summary>
    public static void InitializeKaleidoCaller(this HttpContext httpContext)
    {
        var initializer =
            httpContext.RequestServices
                .GetService<IKaleidoCorrelationContextInitializer>();

        var accessor =
            httpContext.RequestServices
                .GetService<IKaleidoCorrelationContextAccessor>();

        if (initializer is null || accessor is null)
        {
            return;
        }

        var user = httpContext.User;

        // The caller's principal and presented credential, for the outgoing clients: a call made for
        // this caller is authorized as this caller (ADR 0022).
        httpContext.RequestServices
            .GetService<IKaleidoInboundCallerAccessor>()?
            .Set(user, httpContext.Request.Headers.Authorization.ToString());
        var authenticated = user.Identity?.IsAuthenticated == true;
        var callerName =
            authenticated
                ? user.Identity?.Name
                : null;

        // The stable subject id, passed through as the principal provides it: "sub" (as issued by
        // OpenID Connect providers when the handler keeps the claim type), else NameIdentifier.
        var callerId =
            authenticated
                ? (user.FindFirst(SubjectClaimType) ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value
                : null;

        // The application acting for the caller on this hop, from the principal only (ADR 0022).
        var callerActor = authenticated ? ActorOf(user) : null;
        Activity.Current?.SetTag(KaleidoTelemetryTags.CallerActor, callerActor);

        initializer.Initialize(
            accessor.Current with
            {
                CallerName = callerName,
                CallerId = callerId,
                CallerActor = callerActor,
                CallerRoles = callerName is null ? [] : RolesOf(user),
            });
    }

    // The roles exactly as ASP.NET's ClaimsPrincipal.IsInRole (RequireRole) sees them: each identity's
    // own RoleClaimType (ClaimTypes.Role by default, e.g. "roles" for a JWT handler that keeps claim
    // names), so the typed routes and Kaleido's own evaluation (generic execute, discovery) agree (#433).
    private static string[] RolesOf(ClaimsPrincipal user) =>
        user.Identities
            .SelectMany(identity => identity.FindAll(identity.RoleClaimType))
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    // RFC 8693 "act" (a JSON object naming the actor's "sub", or a plain value), else the authorized
    // party ("azp", OpenID Connect) or Entra ID's "appid".
    private static string? ActorOf(ClaimsPrincipal user)
    {
        if (user.FindFirst("act")?.Value is { } act)
        {
            return SubjectOf(act);
        }

        return user.FindFirst("azp")?.Value ?? user.FindFirst("appid")?.Value;
    }

    private static string SubjectOf(string act)
    {
        if (!act.TrimStart().StartsWith('{'))
        {
            return act;
        }

        try
        {
            using var document = JsonDocument.Parse(act);

            return document.RootElement.TryGetProperty("sub", out var subject) && subject.ValueKind == JsonValueKind.String
                ? subject.GetString() ?? act
                : act;
        }
        catch (JsonException)
        {
            return act;
        }
    }
}
