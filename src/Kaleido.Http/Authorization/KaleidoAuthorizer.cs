using Kaleido.Process.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Authorization;

/// <summary>
/// Evaluates <see cref="AuthorizationMetadata"/> requirements against the
/// current request's claims principal — <c>Roles</c> via
/// <c>User.IsInRole</c>, <c>Policy</c> via <c>IAuthorizationService</c>.
/// Used on dynamic code paths where endpoint-level
/// <c>RequireAuthorization</c> cannot express the requirement
/// (multi-step execute) and to filter discovery payloads per caller.
/// </summary>
internal interface IKaleidoAuthorizer
{
    /// <summary>
    /// Returns whether the caller may access the capability. A null
    /// <paramref name="authorization"/> means undeclared — open unless
    /// <see cref="KaleidoHttpOptions.RequireAuthorization"/> is set, in
    /// which case the caller must be authenticated.
    /// </summary>
    Task<bool> CanAccessAsync(
        HttpContext context,
        AuthorizationMetadata? authorization,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Like <see cref="CanAccessAsync"/> but throws
    /// <see cref="KaleidoAuthorizationException"/> (401/403) when denied.
    /// </summary>
    Task AuthorizeAsync(
        HttpContext context,
        AuthorizationMetadata? authorization,
        string capabilityName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Filters a collection to the items whose selected
    /// <see cref="AuthorizationMetadata"/> the caller may access. Used by
    /// catalog/registry endpoints to scope discovery to the caller's persona.
    /// </summary>
    Task<IReadOnlyList<T>> FilterAsync<T>(
        HttpContext context,
        IEnumerable<T> items,
        Func<T, AuthorizationMetadata?> authorization,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enforces process ownership on a loaded <see cref="ProcessorContext"/>.
    /// Unowned processes pass; owned processes require the owner or a caller
    /// sharing an <c>OwnerRoles</c> entry. Throws
    /// <see cref="KaleidoAuthorizationException"/> when denied.
    /// </summary>
    void AuthorizeProcess(
        HttpContext context,
        ProcessorContext processContext);
}

internal sealed class KaleidoAuthorizer(
    KaleidoHttpOptions options,
    ILogger<KaleidoAuthorizer> logger)
    : IKaleidoAuthorizer
{
    public async Task<bool> CanAccessAsync(
        HttpContext context,
        AuthorizationMetadata? authorization,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var user = context.User;

        if (authorization is null)
        {
            return !options.RequireAuthorization
                || user.Identity?.IsAuthenticated == true;
        }

        if (authorization.Roles.Count > 0
            && !authorization.Roles.Any(user.IsInRole))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(authorization.Policy))
        {
            var authorizationService =
                context.RequestServices.GetService<IAuthorizationService>();

            if (authorizationService is null)
            {
                // No authorization pipeline — the policy can never be
                // satisfied. Fail closed (deny) rather than poison discovery
                // endpoints; endpoint-level RequireAuthorization still
                // surfaces the misconfiguration on direct invocation.
                logger.LogWarning(
                    "Capability authorization requires policy '{Policy}' but no IAuthorizationService is registered — denying access. Call AddAuthorization() on the host's service collection.",
                    authorization.Policy);

                return false;
            }

            var result =
                await authorizationService.AuthorizeAsync(
                    user,
                    null,
                    authorization.Policy);

            if (!result.Succeeded)
            {
                return false;
            }
        }

        return true;
    }

    public async Task AuthorizeAsync(
        HttpContext context,
        AuthorizationMetadata? authorization,
        string capabilityName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityName);

        if (!await CanAccessAsync(context, authorization, cancellationToken))
        {
            throw new KaleidoAuthorizationException(
                capabilityName,
                context.User.Identity?.IsAuthenticated == true);
        }
    }

    public async Task<IReadOnlyList<T>> FilterAsync<T>(
        HttpContext context,
        IEnumerable<T> items,
        Func<T, AuthorizationMetadata?> authorization,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(authorization);

        var allowed = new List<T>();

        foreach (var item in items)
        {
            if (await CanAccessAsync(
                    context,
                    authorization(item),
                    cancellationToken))
            {
                allowed.Add(item);
            }
        }

        return allowed;
    }

    public void AuthorizeProcess(
        HttpContext context,
        ProcessorContext processContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(processContext);

        if (processContext.Owner is null)
        {
            return;
        }

        var user = context.User;
        var callerName =
            user.Identity?.IsAuthenticated == true
                ? user.Identity.Name
                : null;
        var authenticated = callerName is not null;

        if (callerName is not null
            && string.Equals(
                callerName,
                processContext.Owner,
                StringComparison.Ordinal))
        {
            return;
        }

        if (authenticated
            && processContext.OwnerRoles.Count > 0
            && processContext.OwnerRoles.Any(user.IsInRole))
        {
            return;
        }

        throw new KaleidoAuthorizationException(
            $"process '{processContext.ProcessId}'",
            authenticated);
    }
}
