using Kaleido.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Authorization;

/// <summary>
/// HTTP adapter over <see cref="IKaleidoAuthorizationEvaluator"/> — supplies
/// the ambient caller from <see cref="IKaleidoCorrelationContextAccessor"/>
/// and bridges declared policies to ASP.NET's <c>IAuthorizationService</c>.
/// Used on dynamic code paths where endpoint-level
/// <c>RequireAuthorization</c> cannot express the requirement
/// (multi-step execute, resume of a stored step) and to filter discovery
/// payloads per caller.
/// </summary>
internal interface IKaleidoAuthorizer
{
    /// <summary>
    /// <see langword="true"/> when <see cref="KaleidoServiceOptions.AuthorizationMode"/> is not <c>None</c>.
    /// </summary>
    bool IsEnforced { get; }

    /// <summary>
    /// Returns whether the caller may access the capability. Always true when
    /// <see cref="KaleidoServiceOptions.AuthorizationMode"/> is <c>None</c>.
    /// A null <paramref name="authorization"/> (undeclared) requires an
    /// authenticated caller in <c>Authenticated</c> mode and is denied in
    /// <c>ZeroTrust</c> mode.
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

internal sealed class KaleidoAuthorizer(
    IKaleidoAuthorizationEvaluator evaluator,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    IHttpContextAccessor httpContextAccessor)
    : IKaleidoAuthorizer
{
    public bool IsEnforced => evaluator.IsEnforced;

    public Task<bool> CanAccessAsync(
        AuthorizationMetadata? authorization,
        CancellationToken cancellationToken) =>
        evaluator.CanAccessAsync(
            authorization,
            correlationAccessor.Current,
            PolicyEvaluator(),
            cancellationToken);

    public Task AuthorizeAsync(
        AuthorizationMetadata? authorization,
        string capabilityName,
        CancellationToken cancellationToken) =>
        evaluator.AuthorizeAsync(
            authorization,
            capabilityName,
            correlationAccessor.Current,
            PolicyEvaluator(),
            cancellationToken);

    public async Task<IReadOnlyList<T>> FilterAsync<T>(
        IEnumerable<T> items,
        Func<T, AuthorizationMetadata?> authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(authorization);

        if (!evaluator.IsEnforced)
        {
            return [.. items];
        }

        var caller = correlationAccessor.Current;
        var policyEvaluator = PolicyEvaluator();

        var allowed = new List<T>();

        // Deliberately a loop, not LINQ (scanners suggest .Where, e.g. CodeQL cs/linq/missed-where):
        // the condition awaits a policy check per item, which Where cannot express, and checks run
        // one after another on purpose - policy handlers may use scoped services that are not
        // thread-safe, so no Task.WhenAll either.
        foreach (var item in items)
        {
            if (await evaluator.CanAccessAsync(
                    authorization(item),
                    caller,
                    policyEvaluator,
                    cancellationToken))
            {
                allowed.Add(item);
            }
        }

        return allowed;
    }

    // The policy bridge stays in the transport — ASP.NET policies evaluate
    // against the live ClaimsPrincipal, which core cannot reconstruct.
    private Func<string, CancellationToken, Task<bool>>? PolicyEvaluator()
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return null;
        }

        var authorizationService =
            httpContext.RequestServices.GetService<IAuthorizationService>();

        return authorizationService is null
            ? null
            : async (policy, _) =>
                (await authorizationService.AuthorizeAsync(
                    httpContext.User,
                    resource: null,
                    policy)).Succeeded;
    }
}
