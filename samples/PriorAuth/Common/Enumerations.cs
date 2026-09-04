using System.Text.Json.Serialization;

namespace Kaleido.Samples.PriorAuth;

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

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LineOfBusiness
{
    Unknown = 0,
    Commercial = 1,
    Medicare = 2,
    Medicaid = 3,
    Exchange = 4
}


[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PriorAuthorizationStatus
{
    Unknown = 0,
    Draft = 1,
    Submitted = 2
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

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MemberGender
{
    Unknown = 0,
    Female = 1,
    Male = 2,
    NonBinary = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RelationshipToSubscriber
{
    Unknown = 0,
    Subscriber = 1,
    Spouse = 2,
    Child = 3,
    OtherDependent = 4
}


[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProcedureModality
{
    Unknown = 0,
    Mri = 1,
    Ct = 2
}


