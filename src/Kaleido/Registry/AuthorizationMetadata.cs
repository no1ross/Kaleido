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
    /// <summary>
    /// Reads <see cref="KaleidoAuthorizationAttribute"/> from a capability type
    /// and projects it to <see cref="AuthorizationMetadata"/>.
    /// Returns <c>null</c> when the type does not declare the attribute.
    /// </summary>
    internal static AuthorizationMetadata? ForType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return
            type.GetCustomAttribute<KaleidoAuthorizationAttribute>() is { } attribute
                ? new AuthorizationMetadata(
                    attribute.Policy,
                    attribute.Roles is { Length: > 0 } roles
                        ? roles.Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries |
                            StringSplitOptions.TrimEntries)
                        : [])
                : null;
    }
}
