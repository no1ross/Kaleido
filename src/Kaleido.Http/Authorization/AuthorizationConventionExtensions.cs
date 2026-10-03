using Microsoft.AspNetCore.Builder;

namespace Kaleido.Http.Authorization;

internal static class AuthorizationConventionExtensions
{
    /// <summary>
    /// For framework endpoints that are not capabilities (execute, transfer):
    /// requires an authenticated caller when
    /// <see cref="KaleidoServiceOptions.EnforceAuthorization"/> is set; attaches
    /// nothing otherwise.
    /// </summary>
    internal static TBuilder RequireKaleidoAuthorization<TBuilder>(
        this TBuilder builder,
        KaleidoServiceOptions options)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithKaleidoAuthorization(null, options);

    /// <summary>
    /// Attaches a capability's <see cref="AuthorizationMetadata"/> requirement
    /// to the endpoint when <see cref="KaleidoServiceOptions.EnforceAuthorization"/>
    /// is set: <c>AllowAnonymous</c> maps to <c>AllowAnonymous()</c>; otherwise an
    /// authenticated caller is required, plus a role requirement for declared
    /// <c>Roles</c> and the consumer's named policy for a declared <c>Policy</c>
    /// (ANDed). When not enforced nothing is attached, so hosts without
    /// authentication keep working.
    /// </summary>
    internal static TBuilder WithKaleidoAuthorization<TBuilder>(
        this TBuilder builder,
        AuthorizationMetadata? authorization,
        KaleidoServiceOptions options)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.EnforceAuthorization)
        {
            return builder;
        }

        if (authorization?.AllowAnonymous == true)
        {
            builder.AllowAnonymous();
            return builder;
        }

        builder.RequireAuthorization();

        if (authorization is null)
        {
            return builder;
        }

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

        return builder;
    }
}
