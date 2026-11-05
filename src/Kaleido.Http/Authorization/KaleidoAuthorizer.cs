using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Authorization;

/// <summary>
/// Capability authorization on the dynamic paths, where endpoint-level <c>RequireAuthorization</c>
/// cannot express the requirement (multi-step execute, resume of a stored step) and to filter discovery
/// payloads per caller. One authorization path (#574): each capability's
/// <see cref="AuthorizationMetadata"/> becomes the same ASP.NET <see cref="AuthorizationPolicy"/> the
/// typed routes attach, checked by <see cref="IAuthorizationService"/> on <c>HttpContext.User</c>, so a
/// role, a policy or the host's own authorization handlers admit the same callers everywhere.
/// </summary>
internal interface IKaleidoAuthorizer
{
    /// <summary>
    /// <see langword="true"/> when <see cref="KaleidoServiceOptions.AuthorizationMode"/> is not <c>None</c>.
    /// </summary>
    bool IsEnforced { get; }

    /// <summary>
    /// Returns whether the caller may access the capability. Always true when
    /// <see cref="KaleidoServiceOptions.AuthorizationMode"/> is <c>None</c>, or the capability declares
    /// <c>AllowAnonymous</c>. A null or empty <paramref name="authorization"/> (undeclared) requires an
    /// authenticated caller in <c>Authenticated</c> mode and is denied in <c>ZeroTrust</c> mode.
    /// </summary>
    Task<bool> CanAccessAsync(
        AuthorizationMetadata? authorization,
        CancellationToken cancellationToken);

    /// <summary>
    /// Like <see cref="CanAccessAsync"/> but throws
    /// <see cref="KaleidoAuthorizationException"/> (401/403) when denied.
    /// </summary>
    Task AuthorizeAsync(
        AuthorizationMetadata? authorization,
        string capabilityName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Filters a collection to the items whose selected
    /// <see cref="AuthorizationMetadata"/> the caller may access. Used by
    /// catalog/registry endpoints to scope discovery to the caller's persona.
    /// </summary>
    Task<IReadOnlyList<T>> FilterAsync<T>(
        IEnumerable<T> items,
        Func<T, AuthorizationMetadata?> authorization,
        CancellationToken cancellationToken);
}

internal sealed partial class KaleidoAuthorizer(
    KaleidoServiceOptions serviceOptions,
    IAuthorizationService authorizationService,
    IAuthorizationPolicyProvider policyProvider,
    IHttpContextAccessor httpContextAccessor,
    ILogger<KaleidoAuthorizer> logger)
    : IKaleidoAuthorizer
{
    // An authenticated caller: what an undeclared capability needs in Authenticated mode.
    private static readonly AuthorizationPolicy Authenticated =
        new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    // Each declared roles-and-policy requirement's own policy, built once (the role part; a named policy
    // comes from the host's provider on each check, as ASP.NET resolves it for a typed route).
    private static readonly ConcurrentDictionary<string, AuthorizationPolicy> RolePolicies = new(StringComparer.Ordinal);

    // The policies this process was asked for and does not define, each logged once.
    private static readonly ConcurrentDictionary<string, byte> UnknownPolicies = new(StringComparer.Ordinal);

    // No request (a call outside one): nobody is authenticated.
    private static readonly ClaimsPrincipal Nobody = new(new ClaimsIdentity());

    public bool IsEnforced =>
        serviceOptions.AuthorizationMode != KaleidoAuthorizationMode.None;

    public async Task<bool> CanAccessAsync(
        AuthorizationMetadata? authorization,
        CancellationToken cancellationToken)
    {
        if (!IsEnforced || authorization?.AllowAnonymous == true)
        {
            return true;
        }

        // Undeclared (or an empty declaration): any authenticated caller in Authenticated mode; denied in
        // ZeroTrust (fail closed - the startup check normally prevents undeclared capabilities being mapped).
        if (authorization?.IsExplicit() != true)
        {
            return serviceOptions.AuthorizationMode != KaleidoAuthorizationMode.ZeroTrust
                && (await authorizationService.AuthorizeAsync(User, resource: null, Authenticated)).Succeeded;
        }

        if (await PolicyForAsync(authorization) is not { } policy)
        {
            return false;
        }

        return (await authorizationService.AuthorizeAsync(User, resource: null, policy)).Succeeded;
    }

    public async Task AuthorizeAsync(
        AuthorizationMetadata? authorization,
        string capabilityName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityName);

        if (!await CanAccessAsync(authorization, cancellationToken))
        {
            throw new KaleidoAuthorizationException(
                capabilityName,
                User.Identity?.IsAuthenticated == true);
        }
    }

    public async Task<IReadOnlyList<T>> FilterAsync<T>(
        IEnumerable<T> items,
        Func<T, AuthorizationMetadata?> authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(authorization);

        if (!IsEnforced)
        {
            return [.. items];
        }

        var allowed = new List<T>();

        // Deliberately a loop, not LINQ (scanners suggest .Where, e.g. CodeQL cs/linq/missed-where):
        // the condition awaits a policy check per item, which Where cannot express, and checks run
        // one after another on purpose - policy handlers may use scoped services that are not
        // thread-safe, so no Task.WhenAll either.
        foreach (var item in items)
        {
            if (await CanAccessAsync(authorization(item), cancellationToken))
            {
                allowed.Add(item);
            }
        }

        return allowed;
    }

    private ClaimsPrincipal User =>
        httpContextAccessor.HttpContext?.User ?? Nobody;

    // The declaration as the typed routes attach it (AuthorizationConventionExtensions): an authenticated
    // caller, in one of its roles (any of them), and satisfying its named policy (ANDed). A policy this host
    // does not define fails closed (#553): the caller is not allowed, and the policy is logged once - a host
    // filtering another service's capabilities (the control registry, MCP) may not define every policy they
    // name, and one of them must not fail the whole read.
    private async Task<AuthorizationPolicy?> PolicyForAsync(AuthorizationMetadata authorization)
    {
        var roles = RolePolicies.GetOrAdd(
            string.Join('\n', authorization.Roles),
            static (_, declared) => declared.Count > 0
                ? new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireRole(declared).Build()
                : Authenticated,
            authorization.Roles);

        if (string.IsNullOrWhiteSpace(authorization.Policy))
        {
            return roles;
        }

        if (await policyProvider.GetPolicyAsync(authorization.Policy) is not { } named)
        {
            if (UnknownPolicies.TryAdd(authorization.Policy, 0))
            {
                LogUnknownPolicy(logger, authorization.Policy);
            }

            return null;
        }

        return AuthorizationPolicy.Combine(roles, named);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authorization policy '{Policy}' is not defined on this host: capabilities requiring it are treated as not allowed. Define it here as the services that name it do.")]
    private static partial void LogUnknownPolicy(ILogger logger, string policy);
}
