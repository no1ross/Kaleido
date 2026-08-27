using System.Text.Json.Serialization;

namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MemberGender
{
    Unknown = 0,
    Female = 1,
    Male = 2,
    NonBinary = 3
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
public enum RelationshipToSubscriber
{
    Unknown = 0,
    Subscriber = 1,
    Spouse = 2,
    Child = 3,
    OtherDependent = 4
}
