using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kaleido.Platform;
using Kaleido.Platform.Registry;

namespace Kaleido.Registry;

// One build's registry entry - a KaleidoRegistry holding that build's processes and query sources - in the
// form a registry store keeps it: serialized with the wire options, and hashed into its revision
// (instances of the same build share it).
internal static class KaleidoRegistryEntryExtensions
{
    public static string ToEntryJson(this KaleidoRegistry entry) =>
        JsonSerializer.Serialize(entry, KaleidoJsonOptions.Options);

    public static KaleidoRegistry ToRegistryEntry(this string json) =>
        JsonSerializer.Deserialize<KaleidoRegistry>(json, KaleidoJsonOptions.Options) ?? new KaleidoRegistry();

    // SHA-256 of the serialized form: the same entry always gives the same revision.
    public static string ToRevision(this string json) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
}
