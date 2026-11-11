using Kaleido.Registry;

namespace Kaleido.Queryable;

/// <summary>Queryable-owned registration settings for sources and views hosted by a service.</summary>
public sealed record KaleidoQueryableOptions : IKaleidoOptions
{
    string IKaleidoOptions.ConfigurationPath => "Kaleido:Queryable";

    void IKaleidoOptions.Validate()
    {
        if (DefaultAuthorization is { } rule)
        {
            AuthorizationMetadata.ValidateDefault(rule);
        }
    }

    /// <summary>
    /// An optional Queryable-specific authorization default for sources without an attribute. When
    /// <see langword="null"/>, Hosting passes the service default; standalone Queryable registration
    /// uses <see cref="AuthorizationMetadata.Unspecified"/>. Configure under
    /// <c>Kaleido:Queryable:DefaultAuthorization</c>.
    /// </summary>
    public AuthorizationMetadata? DefaultAuthorization { get; init; }
}
