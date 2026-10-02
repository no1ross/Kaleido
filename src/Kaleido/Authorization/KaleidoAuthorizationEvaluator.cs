using Kaleido.Processor.Context;
using Kaleido.Registry;
using Microsoft.Extensions.Logging;

namespace Kaleido.Authorization;

/// <summary>
/// Evaluates <see cref="AuthorizationMetadata"/> and process ownership against
/// the caller fields on <see cref="KaleidoCorrelationContext"/>
/// (<see cref="KaleidoCorrelationContext.CallerName"/> /
/// <see cref="KaleidoCorrelationContext.CallerRoles"/> — populated by the
/// transport from the authenticated principal, never headers). An
/// <see cref="KaleidoAuthorizationException"/> is thrown on denial.
/// </summary>
public interface IKaleidoAuthorizationEvaluator
{
    /// <summary>
    /// Returns whether the caller may access the capability. A null
    /// <paramref name="authorization"/> means undeclared — open unless
    /// <paramref name="requireAuthorization"/> is set, in which case the
    /// caller must be authenticated.
    /// <paramref name="policyEvaluator"/> resolves a declared policy name
    /// for the caller — supplied by the transport (HTTP: ASP.NET
    /// <c>IAuthorizationService</c>). <c>null</c> when the transport has no
    /// policy infrastructure; policy-declaring capabilities fail closed with
    /// a warning.
    /// </summary>
    Task<bool> CanAccessAsync(
        AuthorizationMetadata? authorization,
        KaleidoCorrelationContext caller,
        bool requireAuthorization,
        Func<string, CancellationToken, Task<bool>>? policyEvaluator,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Like <see cref="CanAccessAsync"/> but throws
    /// <see cref="KaleidoAuthorizationException"/> when denied.
    /// </summary>
    Task AuthorizeAsync(
        AuthorizationMetadata? authorization,
        string capabilityName,
        KaleidoCorrelationContext caller,
        bool requireAuthorization,
        Func<string, CancellationToken, Task<bool>>? policyEvaluator,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enforces process ownership on a loaded <see cref="ProcessorContext"/> —
    /// unowned processes pass; owned processes require the owner or a caller
    /// sharing an <c>OwnerRoles</c> entry.
    /// </summary>
    void AuthorizeProcess(
        ProcessorContext processContext,
        KaleidoCorrelationContext caller);
}

/// <inheritdoc cref="IKaleidoAuthorizationEvaluator"/>
internal sealed class KaleidoAuthorizationEvaluator(
    ILogger<KaleidoAuthorizationEvaluator> logger)
    : IKaleidoAuthorizationEvaluator
{
    private static bool IsAuthenticated(KaleidoCorrelationContext caller) =>
        caller.CallerName is not null;

    public async Task<bool> CanAccessAsync(
        AuthorizationMetadata? authorization,
        KaleidoCorrelationContext caller,
        bool requireAuthorization,
        Func<string, CancellationToken, Task<bool>>? policyEvaluator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (authorization is null)
        {
            return !requireAuthorization || IsAuthenticated(caller);
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
                logger.LogWarning(
                    "Capability authorization requires policy '{Policy}' but no policy evaluator was supplied — denying access.",
                    authorization.Policy);

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
        bool requireAuthorization,
        Func<string, CancellationToken, Task<bool>>? policyEvaluator,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityName);
        ArgumentNullException.ThrowIfNull(caller);

        if (!await CanAccessAsync(
                authorization,
                caller,
                requireAuthorization,
                policyEvaluator,
                cancellationToken))
        {
            throw new KaleidoAuthorizationException(
                capabilityName,
                IsAuthenticated(caller));
        }
    }

    public void AuthorizeProcess(
        ProcessorContext processContext,
        KaleidoCorrelationContext caller)
    {
        ArgumentNullException.ThrowIfNull(processContext);
        ArgumentNullException.ThrowIfNull(caller);

        if (processContext.Owner is null)
        {
            return;
        }

        if (IsAuthenticated(caller)
            && (string.Equals(
                    caller.CallerName,
                    processContext.Owner,
                    StringComparison.Ordinal)
                || (processContext.OwnerRoles.Count > 0
                    && processContext.OwnerRoles.Any(r =>
                        caller.CallerRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))))
        {
            return;
        }

        throw new KaleidoAuthorizationException(
            $"process '{processContext.ProcessId}'",
            IsAuthenticated(caller));
    }
}
