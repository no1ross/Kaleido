namespace Kaleido.Server;

/// <summary>Transport-neutral capability authorization settings for a Kaleido service.</summary>
public sealed record KaleidoServerOptions : IKaleidoOptions
{
    string IKaleidoOptions.ConfigurationPath => "Kaleido";

    void IKaleidoOptions.Validate() => ValidateAuthorization(this);

    /// <summary>
    /// How capability authorization is enforced. Defaults to <see cref="KaleidoAuthorizationMode.Authenticated"/>;
    /// production hosts should use <see cref="KaleidoAuthorizationMode.ZeroTrust"/> (ADR 0021).
    /// </summary>
    public KaleidoAuthorizationMode AuthorizationMode { get; init; } = KaleidoAuthorizationMode.Authenticated;

    /// <summary>
    /// Service-wide rule for steps and query sources without their own authorization attribute.
    /// An unspecified rule requires an explicit declaration in zero-trust mode.
    /// </summary>
    public AuthorizationMetadata DefaultAuthorization { get; init; } = AuthorizationMetadata.Unspecified;

    internal static void ValidateAuthorization(KaleidoServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Enum.IsDefined(options.AuthorizationMode))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidAuthorizationMode,
                $"AuthorizationMode '{options.AuthorizationMode}' is not supported. Use None, Authenticated, or ZeroTrust.");
        }

        AuthorizationMetadata.ValidateDefault(options.DefaultAuthorization);
    }
}
