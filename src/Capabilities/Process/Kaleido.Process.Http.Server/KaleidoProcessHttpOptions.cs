namespace Kaleido.Process.Http.Server;

/// <summary>Process-specific HTTP response and idempotency settings, separate from generic HTTP filters.</summary>
public sealed record KaleidoProcessHttpOptions : IKaleidoOptions
{
    string IKaleidoOptions.ConfigurationPath => "Kaleido:Process:Http";

    void IKaleidoOptions.Validate() => Validate(this);

    /// <summary>Whether execution responses include framework diagnostics.</summary>
    public bool IncludeFrameworkMessages { get; init; }

    /// <summary>How long a successful idempotent answer is retained for replay.</summary>
    public TimeSpan IdempotencyRetention { get; init; } = TimeSpan.FromHours(24);

    /// <summary>How long an admitted request is considered in progress.</summary>
    public TimeSpan IdempotencyLease { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>How long an unknown outcome prevents a dangerous retry.</summary>
    public TimeSpan IdempotencyUnknownOutcomeRetention { get; init; } = TimeSpan.FromDays(7);

    internal static void Validate(KaleidoProcessHttpOptions options)
    {
        var path = ((IKaleidoOptions)options).ConfigurationPath;
        RequirePositive(options.IdempotencyRetention, nameof(IdempotencyRetention), path);
        RequirePositive(options.IdempotencyLease, nameof(IdempotencyLease), path);
        RequirePositive(options.IdempotencyUnknownOutcomeRetention, nameof(IdempotencyUnknownOutcomeRetention), path);
        if (options.IdempotencyUnknownOutcomeRetention < options.IdempotencyLease)
        {
            throw new KaleidoConfigurationException(ConfigurationErrorCodes.InvalidHttpOptions,
                $"{path}:{nameof(IdempotencyUnknownOutcomeRetention)} must be at least {path}:{nameof(IdempotencyLease)}.");
        }
    }

    private static void RequirePositive(TimeSpan value, string name, string path)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new KaleidoConfigurationException(ConfigurationErrorCodes.InvalidHttpOptions, $"{path}:{name} must be positive.");
        }
    }
}
