using System.Security.Cryptography;
using System.Text;

namespace Kaleido.Platform.Registry;

// Composes the registry a consumer reads (#553): each processor and query source carries its service
// version's overall status, so one call says what can be used and whether it is working. The control
// registry composes it from its store; a participant, from the capabilities and the availability it keeps
// fresh. The composed revision follows both parts, so a host rebuilds what it serves exactly when either
// changes.
internal static class KaleidoRegistryAvailabilityExtensions
{
    public static KaleidoRegistry WithAvailability(this KaleidoRegistry registry, KaleidoRegistryAvailability availability)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(availability);

        var revision = registry.Revision is { } capabilities && availability.Revision is { } versions
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{capabilities}|{versions}"))).ToLowerInvariant()
            : null;

        if (availability.Services.Count == 0)
        {
            return registry with { Revision = revision, GeneratedAt = availability.GeneratedAt };
        }

        var byVersion = availability.Services.ToDictionary(x => Key(x.ServiceName, x.ServiceVersion), x => x.Status, StringComparer.Ordinal);

        // A capability whose version the availability does not list has no instance anyone knows of.
        RegistryCapabilityStatus Of(string service, string? version) =>
            byVersion.GetValueOrDefault(Key(service, version), RegistryCapabilityStatus.Unavailable);

        return registry with
        {
            Processes = [.. registry.Processes.Select(x => x with { Status = Of(x.ServiceName, x.ServiceVersion) })],
            Queryables = [.. registry.Queryables.Select(x => x with { Status = Of(x.ServiceName, x.ServiceVersion) })],
            Revision = revision,
            GeneratedAt = availability.GeneratedAt,
        };
    }

    private static string Key(string service, string? version) => $"{service.ToLowerInvariant()}/{version}";
}
