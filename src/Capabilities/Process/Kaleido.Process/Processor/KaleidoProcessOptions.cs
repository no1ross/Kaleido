using Kaleido.Registry;

namespace Kaleido.Processor;

/// <summary>Process-owned registration settings applied when a service hosts Process steps.</summary>
public sealed record KaleidoProcessOptions : IKaleidoOptions
{
    string IKaleidoOptions.ConfigurationPath => "Kaleido:Process";

    void IKaleidoOptions.Validate()
    {
        if (DefaultAuthorization is { } rule)
        {
            AuthorizationMetadata.ValidateDefault(rule);
        }
    }

    /// <summary>
    /// Marks this processor as the application workflow's entry point. The registry presents this
    /// processor as the one consumers should begin with; only one in a distributed system should be
    /// designated. <c>Kaleido:Process:IsEntryProcessor</c> overrides the value from code.
    /// </summary>
    public bool IsEntryProcessor { get; init; }

    /// <summary>
    /// An optional Process-specific authorization default for steps without an attribute. When
    /// <see langword="null"/>, Hosting passes the service default; standalone Process registration
    /// uses <see cref="AuthorizationMetadata.Unspecified"/>. Configure under
    /// <c>Kaleido:Process:DefaultAuthorization</c>.
    /// </summary>
    public AuthorizationMetadata? DefaultAuthorization { get; init; }
}
