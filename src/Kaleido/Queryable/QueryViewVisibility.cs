using System.Text.Json.Serialization;

namespace Kaleido.Queryable;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QueryViewVisibility
{
    Public = 0,
    Internal = 1
}
