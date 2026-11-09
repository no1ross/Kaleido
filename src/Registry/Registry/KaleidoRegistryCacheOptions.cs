namespace Kaleido.Registry;

/// <summary>Cache settings for the central registry, bound from <c>Kaleido:Registry:Cache</c>.</summary>
public sealed record KaleidoRegistryCacheOptions
{
    /// <summary>The key namespace; choose a different prefix for registry environments sharing a distributed cache.</summary>
    public string KeyPrefix { get; init; } = "kaleido:registry";

    /// <summary>How long a merged registry view is cached. Default one second.</summary>
    public TimeSpan ViewTtl { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>The local view lifetime. When unset, uses <see cref="ViewTtl"/>.</summary>
    public TimeSpan? ViewLocalTtl { get; init; }

    /// <summary>How long an immutable capability revision is retained before it may be read from the store again. Default one year.</summary>
    public TimeSpan CapabilitiesTtl { get; init; } = TimeSpan.FromDays(365);

    /// <summary>The local capability lifetime. When unset, uses <see cref="CapabilitiesTtl"/>.</summary>
    public TimeSpan? CapabilitiesLocalTtl { get; init; }

    internal static void Validate(KaleidoRegistryCacheOptions options)
    {
        Require(!string.IsNullOrWhiteSpace(options.KeyPrefix) && options.KeyPrefix.Length <= 900, nameof(KeyPrefix), "must be non-empty and at most 900 characters");
        Require(options.ViewTtl > TimeSpan.Zero, nameof(ViewTtl), "must be positive");
        Require(options.CapabilitiesTtl > TimeSpan.Zero, nameof(CapabilitiesTtl), "must be positive");
        Require(options.ViewLocalTtl is null || (options.ViewLocalTtl > TimeSpan.Zero && options.ViewLocalTtl <= options.ViewTtl), nameof(ViewLocalTtl), "must be positive and no longer than ViewTtl");
        Require(options.CapabilitiesLocalTtl is null || (options.CapabilitiesLocalTtl > TimeSpan.Zero && options.CapabilitiesLocalTtl <= options.CapabilitiesTtl), nameof(CapabilitiesLocalTtl), "must be positive and no longer than CapabilitiesTtl");
    }

    private static void Require(bool condition, string name, string rule)
    {
        if (!condition)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidOptions,
                $"Kaleido:Registry:Cache:{name} {rule}.");
        }
    }
}
