using Microsoft.AspNetCore.Http;
namespace Kaleido.Http;

/// <summary>
/// Options for the Kaleido HTTP transport. Kaleido performs no authentication
/// itself — these options shape how inbound correlation headers are
/// interpreted against the host's authentication middleware. Capability
/// authorization is controlled by
/// <see cref="KaleidoServiceOptions.AuthorizationMode"/>.
/// </summary>
/// <remarks>
/// Immutable: <c>AddHttp</c> lays the <c>Kaleido:Http</c> configuration section over a copy of the
/// instance passed in code (configuration overrides code, setting by setting), validates it and
/// registers it (ADR 0030). <see cref="TrustCorrelationIdentity"/> can only be set in code.
/// </remarks>
public sealed record KaleidoHttpOptions
{
    /// <summary>The configuration section that overrides these options: <c>Kaleido:Http</c>.</summary>
    public const string SectionName = "Kaleido:Http";

    /// <summary>
    /// Decides whether identity-bearing correlation headers
    /// (<c>X-Kaleido-Request-Id</c>, <c>X-Kaleido-Calling-Processor</c>,
    /// <c>X-Kaleido-Calling-Step</c>)
    /// are honored for a request. <c>X-Kaleido-Process-Id</c> is always
    /// honored — it is a resumable process handle, not an identity claim.
    /// <para>
    /// When <see langword="null"/> (default): trusted when the host has no
    /// authentication infrastructure registered (nothing to check
    /// against — preserves pre-auth behavior), otherwise requires an
    /// authenticated caller.
    /// </para>
    /// </summary>
    public Func<HttpContext, bool>? TrustCorrelationIdentity { get; init; }

    /// <summary>
    /// Includes framework-generated diagnostics in Process execution responses when enabled.
    /// Off by default: <c>FrameworkMessages</c> stays empty, while handler-authored
    /// <c>BusinessMessages</c> is always returned.
    /// </summary>
    public bool IncludeFrameworkMessages { get; init; }

    /// <summary>
    /// How long the answer to an execute request sent with an <c>Idempotency-Key</c> is kept, so a retry
    /// with the same key gets it again instead of running the step a second time. Default 24 hours.
    /// </summary>
    public TimeSpan IdempotencyRetention { get; init; } = TimeSpan.FromHours(24);

    /// <summary>
    /// How long a request sent with an <c>Idempotency-Key</c> counts as running. Meanwhile a retry with
    /// the same key does not run (409 <c>pro_idempotency_in_progress</c>, with <c>Retry-After</c>). If
    /// the request has not finished by then (e.g. the service stopped), a retry is answered 409
    /// <c>pro_idempotency_outcome_unknown</c> and never run again with that key. Default 5 minutes;
    /// keep it longer than a step can take.
    /// </summary>
    public TimeSpan IdempotencyLease { get; init; } = TimeSpan.FromMinutes(5);

    // Run by AddHttp after configuration is laid over the options; nothing is adjusted, an invalid
    // value stops startup.
    internal static void Validate(KaleidoHttpOptions options)
    {
        Require(options.IdempotencyRetention > TimeSpan.Zero, nameof(IdempotencyRetention), options.IdempotencyRetention);
        Require(options.IdempotencyLease > TimeSpan.Zero, nameof(IdempotencyLease), options.IdempotencyLease);
    }

    private static void Require(bool condition, string name, TimeSpan value)
    {
        if (!condition)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidHttpOptions,
                $"{SectionName}:{name} is {value}; it must be positive.");
        }
    }
}
