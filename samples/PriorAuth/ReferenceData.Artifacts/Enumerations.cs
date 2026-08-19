using System.Text.Json.Serialization;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LineOfBusiness
{
    Unknown = 0,
    Commercial = 1,
    Medicare = 2,
    Medicaid = 3,
    Exchange = 4
}