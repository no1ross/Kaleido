using System.Reflection;

namespace Kaleido.Registry;

/// <summary>
/// The resolved authorization requirement for a capability, projected from
/// <see cref="KaleidoAuthorizationAttribute"/> onto registry metadata.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AuthorizationMetadata(
    string? Policy,
    IReadOnlyList<string> Roles)
{
    public static AuthorizationMetadata Unspecified { get; } = new(null, []);

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
    /// Returns <c>null</c> when the type does not declare the attribute.
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
            AllowAnonymous = attribute.AllowAnonymous
        };
    }
}
