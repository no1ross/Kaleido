using System.Security.Cryptography;
using System.Text.Json;
using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;

namespace Kaleido.Http.Registry;

// What one build of a service version publishes, in the form a registry store keeps: serialized with the
// wire options, and hashed into its revision (instances of the same build share it).
internal sealed record RegistryCapabilities(
    IReadOnlyCollection<ProcessorRegistryResponse> Processes,
    IReadOnlyCollection<QueryableSourceResponse> Queryables)
{
    public string ToJson() => JsonSerializer.Serialize(this, KaleidoJsonOptions.Options);

    public static RegistryCapabilities FromJson(string json) =>
        JsonSerializer.Deserialize<RegistryCapabilities>(json, KaleidoJsonOptions.Options)
        ?? new RegistryCapabilities([], []);

    // SHA-256 of the serialized form: the same capabilities always give the same revision.
    public static string RevisionOf(string json) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
}
