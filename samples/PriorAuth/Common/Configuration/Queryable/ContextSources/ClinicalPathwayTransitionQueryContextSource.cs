using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.Configuration.Data;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.ContextSources;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Clinical Pathway Transitions",
    Description = "Which questionnaire a clinical pathway asks next after a questionnaire's answers, or that it is done.",
    Source = "Prior Authorization Configuration")]
internal sealed class ClinicalPathwayTransitionQueryContextSource(
    ConfigurationDbContext dbContext)
    : IQuerySource<ClinicalPathwayTransitionQueryContext>
{
    public IQueryable<ClinicalPathwayTransitionQueryContext> CreateQuery(
        QueryExecutionContext executionContext) =>
        dbContext.ClinicalPathwayTransitions
            .AsNoTracking()
            .Select(x => new ClinicalPathwayTransitionQueryContext
            {
                ClinicalPathwayTransitionId = x.ClinicalPathwayTransitionId,
                FromQuestionnaireId = x.FromQuestionnaireId,
                WhenBindingKey = x.WhenBindingKey,
                Operator = x.Operator,
                AnswerValue = x.AnswerValue,
                ToQuestionnaireId = x.ToQuestionnaireId,
                Determination = x.Determination,
                Priority = x.Priority
            });
}