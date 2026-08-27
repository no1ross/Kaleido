using Kaleido.Samples.PriorAuth.CodeSet.Artifacts;
using System.Text.Json.Serialization;

namespace Kaleido.Samples.PriorAuth.Configuration.Artifacts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProcedureModality
{
    Unknown = 0,
    Mri = 1,
    Ct = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MriBodyPart
{
    Unknown = 0,
    Spine = 1,
    Knee = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Laterality
{
    Unknown = 0,
    None = 1,
    Left = 2,
    Right = 3,
    Bilateral = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContrastOption
{
    Unknown = 0,
    WithoutContrast = 1,
    WithContrast = 2,
    WithAndWithoutContrast = 3
}
