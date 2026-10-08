using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.Configuration.Data;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.ContextSources;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Questionnaire Definitions",
    Description = "Clinical questionnaire definitions, by questionnaire id and version.",
    Source = "Prior Authorization Configuration")]
internal sealed class QuestionnaireDefinitionQueryContextSource(
    ConfigurationDbContext dbContext)
    : IQuerySource<QuestionnaireDefinitionQueryContext>
{
    public IQueryable<QuestionnaireDefinitionQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.QuestionnaireDefinitions
            .AsNoTracking()
            .Select(definition =>
                new QuestionnaireDefinitionQueryContext
                {
                    QuestionnaireDefinitionId = definition.QuestionnaireDefinitionId,
                    QuestionnaireId = definition.QuestionnaireId,
                    Version = definition.Version,
                    Name = definition.Name,
                    Title = definition.Title,
                    IsActive = definition.IsActive
                });
    }
}
