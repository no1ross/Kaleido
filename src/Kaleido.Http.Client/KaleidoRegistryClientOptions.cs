using System.Globalization;
using Kaleido.Exceptions;

namespace Kaleido.Http.Client;

/// <summary>
/// How a Kaleido host takes part in the control registry (#553), bound from <c>Kaleido:Registry</c>: where it
/// is, and the address this version is reached at. How often the host heartbeats and checks the registry is
/// the registry's to decide: it hands the intervals out, and the host follows them.
/// </summary>
/// <remarks>
/// <code>
/// "Kaleido": { "Registry": { "Url": "https://registry.internal", "Address": "https://code-sets.internal" } }
/// </code>
/// </remarks>
public sealed record KaleidoRegistryClientOptions
{
    /// <summary>The configuration section, below <c>Kaleido</c>.</summary>
    public const string SectionName = "Registry";

    /// <summary>The control registry's base URL, absolute http or https. Required.</summary>
    public string? Url { get; init; }

    /// <summary>The address this service version is reached at, registered with each instance.</summary>
    public string? Address { get; init; }

    /// <summary>
    /// The control registry's service name, as this host's call credentials know it (the
    /// <c>ServiceName</c> of the credential request for its calls). Default <c>registry</c>.
    /// </summary>
    public string ServiceName { get; init; } = "registry";

    internal static void Validate(KaleidoRegistryClientOptions options)
    {
        Require(Uri.TryCreate(options.Url, UriKind.Absolute, out var url) && url.Scheme is "http" or "https", nameof(Url), "must be an absolute http or https URL, e.g. 'https://registry.internal'");
        Require(options.Address is null || (Uri.TryCreate(options.Address, UriKind.Absolute, out var address) && address.Scheme is "http" or "https"), nameof(Address), "must be an absolute http or https URL when set");
        Require(KaleidoServiceOptions.IsValidServiceName(options.ServiceName), nameof(ServiceName), "must be a service name");
    }

    private static void Require(bool condition, string name, string rule)
    {
        if (!condition)
        {
            throw new KaleidoConfigurationException(
                HttpClientErrorCodes.InvalidClientOptions,
                string.Create(CultureInfo.InvariantCulture, $"Kaleido:Registry:{name} {rule}."));
        }
    }
}
