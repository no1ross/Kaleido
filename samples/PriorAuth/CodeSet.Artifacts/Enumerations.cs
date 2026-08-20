using System.Text.Json.Serialization;

namespace Kaleido.Samples.PriorAuth.CodeSet.Artifacts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProcedureCodeSystem
{
    Unknown = 0,
    Cpt = 1,
    Hcpcs = 2,
    Local = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiagnosisCodeSystem
{
    Unknown = 0,
    Icd10Cm = 1,
    Local = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GroupingType
{
    Unknown = 0,
    Authorization = 1,
    Benefit = 2,
    Clinical = 3,
    Administrative = 4,
    Other = 5
}
