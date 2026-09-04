using Kaleido.Samples.PriorAuth.Configuration.Process.Models;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Models;

public sealed record CaptureRequestedServiceResponse
{
    public string? QuestionnaireId { get; init; }

    public string? QuestionnaireVersion { get; init; }

    public QuestionnaireDefinitionRecord? Questionnaire { get; init; }
}
