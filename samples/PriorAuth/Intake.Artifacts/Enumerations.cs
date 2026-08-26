using System.Text.Json.Serialization;
using Kaleido.Samples.PriorAuth.CodeSet.Artifacts;
using Kaleido.Samples.PriorAuth.MemberService.Artifacts;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PriorAuthorizationStatus
{
    Unknown = 0,
    Draft = 1,
    Submitted = 2
}

