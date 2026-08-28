using System.Text.Json.Serialization;

namespace Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProviderIdentifierType
{
    Unknown = 0,
    TIN = 1,
    NPI = 2,
    Internal = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProviderType
{
    Unknown = 0,
    RequestingProvider = 1,
    ServicingFacility = 2
}
