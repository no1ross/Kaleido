using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Views;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Models;

public sealed record StartRadiologyIntakeResponse
{
    public string? QuestionnaireId { get; init; }

    public string? QuestionnaireVersion { get; init; }

    public QuestionnaireDefinitionView? Questionnaire { get; init; }
}
