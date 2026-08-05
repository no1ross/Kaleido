using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Runtime;

namespace Kaleido.Process.FunctionalTests.Tests.Runtime;

internal static class RuntimeRequestFactory
{
    public static ProcessRequest Create(
        string participantProcessId,
        string requestId,
        params string[] stepNames)
    {
        return new ProcessRequest
        {
            ParticipantProcessId = participantProcessId,
            RequestId = requestId,
            Participant = new ParticipantRequest
            {
                Steps = stepNames.ToDictionary(
                    x => x,
                    _ => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(),
                    StringComparer.OrdinalIgnoreCase)
            }
        };
    }
}
