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
        var initializer =
            context.HttpContext.RequestServices
                .GetService<IKaleidoCorrelationContextInitializer>();

        var accessor =
            context.HttpContext.RequestServices
                .GetService<IKaleidoCorrelationContextAccessor>();

        if (initializer is not null && accessor is not null)
        {
            var user = context.HttpContext.User;
            var callerName =
                user.Identity?.IsAuthenticated == true
                    ? user.Identity.Name
                    : null;

            initializer.Initialize(
                accessor.Current with
                {
                    CallerName = callerName,
                    CallerRoles =
                        callerName is null
                            ? []
                            : user.Claims
                                .Where(x => string.Equals(
                                    x.Type,
                                    ClaimTypes.Role,
                                    StringComparison.Ordinal))
                                .Select(x => x.Value)
                                .ToArray()
                });
        }

        return await next(context);
    }
}
