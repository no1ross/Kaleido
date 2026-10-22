using Kaleido.Registry;
using Microsoft.Extensions.Logging;

namespace Kaleido.Authorization;

/// <summary>
/// Evaluates the <see cref="AuthorizationMetadata"/> declared on a step or
/// query source against the caller fields on <see cref="KaleidoCorrelationContext"/>
/// (<see cref="KaleidoCorrelationContext.CallerName"/> /
/// <see cref="KaleidoCorrelationContext.CallerRoles"/> — populated by the
/// transport from the authenticated principal, never headers). An
/// <see cref="KaleidoAuthorizationException"/> is thrown on denial.
/// Nothing is enforced when <see cref="KaleidoServiceOptions.AuthorizationMode"/>
/// is <see cref="KaleidoAuthorizationMode.None"/>.
/// </summary>
/// <remarks>
/// Kaleido has no case-level guardrails: whether a caller may open or continue
/// a particular process is a business/data-level concern of the underlying
/// systems (ADR 0010). The evaluator only checks the caller's identity and the
/// roles/policies declared on the capability being called.
/// </remarks>
internal interface IKaleidoAuthorizationEvaluator
{
    /// <summary>
    /// <see langword="true"/> when <see cref="KaleidoServiceOptions.AuthorizationMode"/> is
    /// not <see cref="KaleidoAuthorizationMode.None"/>.
    /// </summary>
    bool IsEnforced { get; }

    /// <summary>
    /// Returns whether the caller may access the capability. When enforced, the
    /// caller must be authenticated unless the capability declares
    /// <c>AllowAnonymous</c>, plus any declared roles (any-of) and policy.
    /// A null or empty <paramref name="authorization"/> means undeclared: any
    /// authenticated caller in <see cref="KaleidoAuthorizationMode.Authenticated"/>
    /// mode, denied in <see cref="KaleidoAuthorizationMode.ZeroTrust"/> mode.
    /// <paramref name="policyEvaluator"/> resolves a declared policy name
    /// for the caller — supplied by the transport (HTTP: ASP.NET
    /// <c>IAuthorizationService</c>). <see langword="null"/> when the transport has no
    /// policy infrastructure; policy-declaring capabilities fail closed with
    /// a warning.
    /// </summary>
    Task<bool> CanAccessAsync(
        AuthorizationMetadata? authorization,
        KaleidoCorrelationContext caller,
        Func<string, CancellationToken, Task<bool>>? policyEvaluator,
        CancellationToken cancellationToken);

    /// <summary>
    /// Like <see cref="CanAccessAsync"/> but throws
    /// <see cref="KaleidoAuthorizationException"/> when denied.
    /// </summary>
    Task AuthorizeAsync(
        AuthorizationMetadata? authorization,
        string capabilityName,
        KaleidoCorrelationContext caller,
        Func<string, CancellationToken, Task<bool>>? policyEvaluator,
        CancellationToken cancellationToken);
}

/// <inheritdoc cref="IKaleidoAuthorizationEvaluator"/>
internal sealed partial class KaleidoAuthorizationEvaluator(
    KaleidoServiceOptions serviceOptions,
    ILogger<KaleidoAuthorizationEvaluator> logger)
    : IKaleidoAuthorizationEvaluator
{
    private static bool IsAuthenticated(KaleidoCorrelationContext caller) =>
        caller.CallerName is not null;

    public bool IsEnforced =>
        serviceOptions.AuthorizationMode != KaleidoAuthorizationMode.None;

    public async Task<bool> CanAccessAsync(
        AuthorizationMetadata? authorization,
        KaleidoCorrelationContext caller,
        Func<string, CancellationToken, Task<bool>>? policyEvaluator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!IsEnforced || authorization?.AllowAnonymous == true)
        {
            return true;
        }

        if (!IsAuthenticated(caller))
        {
            return false;
        }

        // Undeclared (or an empty declaration): any authenticated caller in
        // Authenticated mode; denied in ZeroTrust (fail closed — the startup
        // check normally prevents undeclared capabilities from being mapped).
        if (authorization?.IsExplicit() != true)
        {
            return serviceOptions.AuthorizationMode != KaleidoAuthorizationMode.ZeroTrust;
        }

        if (authorization.Roles.Count > 0
            && !authorization.Roles.Any(r =>
                caller.CallerRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(authorization.Policy))
        {
            if (policyEvaluator is null)
            {
                // No policy infrastructure — the requirement can never be
                // satisfied. Fail closed rather than poison discovery.
                LogNoPolicyEvaluator(logger, authorization.Policy);

                return false;
            }

            if (!await policyEvaluator(authorization.Policy, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    public async Task AuthorizeAsync(
        AuthorizationMetadata? authorization,
        string capabilityName,
        KaleidoCorrelationContext caller,
        Func<string, CancellationToken, Task<bool>>? policyEvaluator,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityName);
        ArgumentNullException.ThrowIfNull(caller);

        if (!await CanAccessAsync(
                authorization,
                caller,
                policyEvaluator,
                cancellationToken))
        {
            throw new KaleidoAuthorizationException(
                capabilityName,
                IsAuthenticated(caller));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Capability authorization requires policy '{Policy}' but no policy evaluator was supplied — denying access.")]
    private static partial void LogNoPolicyEvaluator(ILogger logger, string? policy);
}
