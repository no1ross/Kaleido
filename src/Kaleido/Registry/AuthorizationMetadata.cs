using System.Reflection;

namespace Kaleido.Registry;

/// <summary>
/// The resolved authorization requirement for a capability, projected from
/// <see cref="KaleidoAuthorizationAttribute"/> onto registry metadata.
/// </summary>
/// <param name="Policy">
/// The name of a consumer-defined authorization policy, or <see langword="null"/> when none is required.
/// </param>
/// <param name="Roles">
/// The role names, one of which the caller must be in; empty when no role is required.
/// </param>
public sealed record AuthorizationMetadata(
    string? Policy,
    IReadOnlyList<string> Roles)
{
    /// <summary>
    /// No declaration: no policy, no roles and no <see cref="AllowAnonymous"/>. The default of
    /// <c>KaleidoServiceOptions.DefaultAuthorization</c> and of a capability's effective rule when
    /// neither the capability nor its service declared one; it is not <see cref="IsExplicit"/>.
    /// </summary>
    public static AuthorizationMetadata Unspecified { get; } = new(Policy: null, []);

    /// <summary>
    /// The capability is open to unauthenticated callers when authorization is
    /// enforced. Never combined with <see cref="Roles"/> or <see cref="Policy"/>.
    /// </summary>
    public bool AllowAnonymous { get; init; }

    /// <summary>
    /// The declaration says who may access the capability: anonymous callers,
    /// specific roles, or a policy. An empty declaration is not explicit.
    /// Zero-trust mode requires every capability to be explicit.
    /// </summary>
    public bool IsExplicit() =>
        AllowAnonymous
        || Roles.Count > 0
        || !string.IsNullOrWhiteSpace(Policy);

    /// <summary>
    /// Reads <see cref="KaleidoAuthorizationAttribute"/> from a capability type
    /// and projects it to <see cref="AuthorizationMetadata"/>.
    /// Returns <see langword="null"/> when the type does not declare the attribute.
    /// </summary>
    /// <exception cref="KaleidoConfigurationException">
    /// <c>AllowAnonymous</c> is declared together with roles or a policy.
    /// </exception>
    internal static AuthorizationMetadata ForType(Type type, AuthorizationMetadata fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        return ForType(type) ?? fallback;
    }

    internal static AuthorizationMetadata? ForType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.GetCustomAttribute<KaleidoAuthorizationAttribute>() is not { } attribute)
        {
            return null;
        }

        var roles =
            attribute.Roles is { Length: > 0 } declared
                ? declared.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                : [];

        if (attribute.AllowAnonymous
            && (roles.Length > 0 || !string.IsNullOrWhiteSpace(attribute.Policy)))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.ConflictingAuthorization,
                $"[KaleidoAuthorization] on '{type.Name}' declares AllowAnonymous together with Roles or Policy. " +
                "Use AllowAnonymous alone, or remove it.");
        }

        return new AuthorizationMetadata(attribute.Policy, roles)
        {
            AllowAnonymous = attribute.AllowAnonymous,
        };
    }
}
