using Kaleido.Samples.PriorAuth.Configuration.Artifacts.Process.Models;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;

public sealed record CaptureRequestedServiceResponse
{
    public string? QuestionnaireId { get; init; }

    public string? QuestionnaireVersion { get; init; }

    public QuestionnaireDefinitionRecord? Questionnaire { get; init; }
}
