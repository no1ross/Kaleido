using Kaleido.Http.Queryable;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Parameters;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Views;

namespace Kaleido.Samples.PriorAuth.Questionnaires;

/// <summary>
/// Reads questionnaires and clinical pathways from the Configuration service, for any specialty
/// processor (Radiology today; others later). The business owns the content; this only fetches it.
/// </summary>
public sealed class ConfiguredQuestionnaireClient(
    IKaleidoQueryableClientFactory queryableClientFactory)
{
    private const string ConfigurationClient = "Configuration";

    /// <summary>The questionnaire the rules select for a step and context, or a specific one by id.</summary>
    public async Task<QuestionnaireDefinitionView?> FindAsync(
        QuestionnaireDefinitionViewParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = await queryableClientFactory
            .GetClient(ConfigurationClient)
            .QueryViewAsync<QuestionnaireDefinitionViewParameters, QuestionnaireDefinitionView>(
                "QuestionnaireDefinitionQueryContextSource",
                "QuestionnaireDefinitionViewSource",
                new QueryApiRequest<QuestionnaireDefinitionViewParameters> { Parameters = parameters },
                cancellationToken);

        return result.Results.SingleOrDefault();
    }

    /// <summary>The transitions out of a questionnaire, highest priority first.</summary>
    public async Task<IReadOnlyList<ClinicalPathwayTransitionQueryContext>> TransitionsAsync(
        string fromQuestionnaireId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryableClientFactory
            .GetClient(ConfigurationClient)
            .QuerySourceAsync<ClinicalPathwayTransitionQueryContext>(
                "ClinicalPathwayTransitionQueryContextSource",
                new QueryApiRequest(
                    new QueryApiBody
                    {
                        Filter = new QueryApiFilterNode
                        {
                            Condition = new QueryApiFilterCondition
                            {
                                Field = nameof(ClinicalPathwayTransitionQueryContext.FromQuestionnaireId),
                                Operator = "equals",
                                Values = [System.Text.Json.JsonSerializer.SerializeToElement(fromQuestionnaireId)]
                            }
                        }
                    }),
                cancellationToken);

        return result.Results.OrderByDescending(x => x.Priority).ToArray();
    }
}
