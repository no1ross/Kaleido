namespace Kaleido.Http;

/// <summary>Builds the versioned service route shared by Kaleido HTTP adapters.</summary>
public static class KaleidoHttpRoutePrefix
{
    /// <summary>Builds the logical route prefix for one named service version.</summary>
    /// <param name="serviceName">The registered service name.</param>
    /// <param name="serviceVersion">The required version served by this host.</param>
    /// <returns>The relative route <c>/{service}/{version}</c> without a trailing slash.</returns>
    /// <exception cref="ArgumentException">Either identifier is empty or whitespace.</exception>
    public static string Service(string serviceName, string serviceVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceVersion);
        return $"/{serviceName.Trim().Trim('/')}/{serviceVersion.Trim().Trim('/')}";
    }
}
