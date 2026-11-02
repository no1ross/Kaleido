using System.Globalization;

namespace Kaleido.Http.Registry;

/// <summary>
/// The control registry's settings (#553), bound from <c>Kaleido:Registry</c>; every value has a default
/// except who may register.
/// </summary>
/// <remarks>
/// <code>
/// "Kaleido": {
///   "Registry": {
///     "Registrants": { "radiology": [ "radiology-client-id" ], "member": [ "member-client-id" ] },
///     "ReaderPolicy": "KaleidoRegistryReader",
///     "Expected": [ { "ServiceName": "radiology", "Versions": [ "2026-11" ] } ],
///     "HeartbeatInterval": "00:00:10", "MissedHeartbeats": 3, "Retention": "1.00:00:00"
///   }
/// }
/// </code>
/// </remarks>
public sealed record KaleidoRegistryOptions
{
    /// <summary>The configuration section, below <c>Kaleido</c>.</summary>
    public const string SectionName = "Registry";

    /// <summary>
    /// Who may register each service: the service name, mapped to the client ids allowed to register it,
    /// compared with the caller's actor (<c>act</c>, else <c>azp</c>, else <c>appid</c>; else its subject).
    /// A service not listed may not be registered. Not checked when the host's <c>AuthorizationMode</c> is
    /// <c>None</c>.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Registrants { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The authorization policy of the Kaleido hosts that read the control registry (services, MCP, the
    /// gateway): they get the full registry, unfiltered, and filter it for their own callers. No one else
    /// reads it. Required unless the host's <c>AuthorizationMode</c> is <c>None</c>.
    /// </summary>
    public string? ReaderPolicy { get; init; }

    /// <summary>
    /// The processor versions this environment expects: one that has not registered is reported as missing
    /// and makes the registry partial.
    /// </summary>
    public IReadOnlyList<KaleidoRegistryExpectedService> Expected { get; init; } = [];

    /// <summary>How often an instance heartbeats. Default 10 seconds.</summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>The heartbeats an instance may miss before it is not ready. Default 3.</summary>
    public int MissedHeartbeats { get; init; } = 3;

    /// <summary>How long an instance with no heartbeat is kept (and shown) before it is removed. Default 24 hours.</summary>
    public TimeSpan Retention { get; init; } = TimeSpan.FromHours(24);

    /// <summary>How long an instance counts as alive after its last heartbeat.</summary>
    internal TimeSpan Lease => HeartbeatInterval * MissedHeartbeats;

    internal static void Validate(KaleidoRegistryOptions options)
    {
        Require(options.HeartbeatInterval > TimeSpan.Zero, nameof(HeartbeatInterval), "must be positive");
        Require(options.MissedHeartbeats > 0, nameof(MissedHeartbeats), "must be positive");
        Require(options.Retention > options.Lease, nameof(Retention), "must be longer than HeartbeatInterval x MissedHeartbeats");

        foreach (var (service, clients) in options.Registrants)
        {
            Require(KaleidoServiceOptions.IsValidServiceName(service) && clients.Count > 0 && clients.All(x => !string.IsNullOrWhiteSpace(x)), $"{nameof(Registrants)}:{service}", "must name a service and at least one client id");
        }

        foreach (var expected in options.Expected)
        {
            Require(KaleidoServiceOptions.IsValidServiceName(expected.ServiceName) && expected.Versions.Count > 0 && expected.Versions.All(KaleidoServiceOptions.IsValidServiceVersion), $"{nameof(Expected)}:{expected.ServiceName}", "must name a service and at least one valid version");
        }
    }

    private static void Require(bool condition, string name, string rule)
    {
        if (!condition)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.InvalidOptions,
                string.Create(CultureInfo.InvariantCulture, $"Kaleido:Registry:{name} {rule}."));
        }
    }
}

/// <summary>A processor this environment expects, with the versions that must be registered.</summary>
[ExcludeFromCodeCoverage]
public sealed record KaleidoRegistryExpectedService
{
    /// <summary>The service.</summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>The versions that must be registered.</summary>
    public IReadOnlyList<string> Versions { get; init; } = [];
}
