using System.Security.Claims;
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

        initializer.Initialize(
            accessor.Current with
            {
                CallerName = callerName,
                CallerId = callerId,
                CallerRoles =
                    callerName is null
                        ? []
                        : user.Claims
                            .Where(x => string.Equals(
                                x.Type,
                                ClaimTypes.Role,
                                StringComparison.Ordinal))
                            .Select(x => x.Value)
                            .ToArray(),
            });
    }
}
