using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Parameters;
using Kaleido.Samples.PriorAuth.Questionnaires;

namespace Kaleido.Samples.PriorAuth.Clinical;

/// <summary>
/// Clinical rounds from the Configuration service: the first questionnaire is selected by the step's
/// mapping rules (plan, line of business, modality, procedure); after each round, the configured
/// transitions of that questionnaire decide the next one, or finish with a determination. The whole
/// pathway is business data.
/// </summary>
public sealed class ConfiguredClinicalPathwayProvider(
    ConfiguredQuestionnaireClient questionnaires)
    : IClinicalQuestionProvider
{
    public async Task<ClinicalRound?> StartAsync(
        ClinicalContext context,
        CancellationToken cancellationToken)
    {
        var first = await questionnaires.FindAsync(
            new QuestionnaireDefinitionViewParameters
            {
                StepName = context.StepName,
                PlanId = context.PlanId,
                LineOfBusiness = context.LineOfBusiness,
                ProcedureModality = context.ProcedureModality,
                ProcedureCodeValue = context.ProcedureCodeValue
            },
            cancellationToken);

        return first is null
            ? null
            : new ClinicalRound(first.QuestionnaireId, QuestionnaireTranslation.ToInformationRequest(first, context.StatedDetails, Tokens(context)));
    }

    public async Task<ClinicalNext> ContinueAsync(
        ClinicalContext context,
        ClinicalRound round,
        IInformationStep answers,
        CancellationToken cancellationToken)
    {
        var transitions = await questionnaires.TransitionsAsync(round.RoundId, cancellationToken);

        if (transitions.FirstOrDefault(x => Matches(x, answers)) is not { } transition)
        {
            return ClinicalNext.Done(null);
        }

        if (string.IsNullOrWhiteSpace(transition.ToQuestionnaireId))
        {
            return ClinicalNext.Done(transition.Determination);
        }

        var next = await questionnaires.FindAsync(
            new QuestionnaireDefinitionViewParameters { QuestionnaireId = transition.ToQuestionnaireId },
            cancellationToken);

        return next is null
            ? ClinicalNext.Done(transition.Determination)
            : new ClinicalNext(new ClinicalRound(next.QuestionnaireId, QuestionnaireTranslation.ToInformationRequest(next, context.StatedDetails, Tokens(context))));
    }

    // Equals: any answer equals the value; NotEquals: no answer equals it; no condition: always.
    private static bool Matches(
        ClinicalPathwayTransitionQueryContext transition,
        IInformationStep answers)
    {
        if (string.IsNullOrWhiteSpace(transition.WhenBindingKey))
        {
            return true;
        }

        var given = QuestionnaireTranslation.Answers(answers, transition.WhenBindingKey);
        var equals = given.Any(x => string.Equals(x, transition.AnswerValue, StringComparison.OrdinalIgnoreCase));

        return string.Equals(transition.Operator, "NotEquals", StringComparison.OrdinalIgnoreCase)
            ? given.Count > 0 && !equals
            : equals;
    }

    private static Dictionary<string, string> Tokens(ClinicalContext context) =>
        new(StringComparer.OrdinalIgnoreCase) { ["ProcedureCode"] = context.ProcedureCodeValue ?? string.Empty };
}
