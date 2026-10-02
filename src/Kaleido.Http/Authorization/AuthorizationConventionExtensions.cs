using Microsoft.AspNetCore.Builder;

namespace Kaleido.Http.Authorization;

internal static class AuthorizationConventionExtensions
{
    /// <summary>
    /// Attaches a capability's <see cref="AuthorizationMetadata"/> requirement
    /// to the endpoint: <c>Roles</c> map to a role requirement,
    /// <c>Policy</c> maps to the consumer's named authorization policy; both
    /// declared are ANDed. When the capability declares nothing and
    /// <see cref="KaleidoHttpOptions.RequireAuthorization"/> is set, the
    /// endpoint requires an authenticated caller.
    /// </summary>
    internal static TBuilder WithKaleidoAuthorization<TBuilder>(
        this TBuilder builder,
        AuthorizationMetadata? authorization,
        KaleidoHttpOptions options)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        if (authorization is not null)
        {
            if (authorization.Roles.Count > 0)
            {
                builder.RequireAuthorization(policy =>
                    policy.RequireRole(
                        [.. authorization.Roles]));
            }

            if (!string.IsNullOrWhiteSpace(authorization.Policy))
            {
                builder.RequireAuthorization(authorization.Policy);
            }
        }
        else if (options.RequireAuthorization)
        {
            builder.RequireAuthorization();
        }

        return builder;
    }
}
