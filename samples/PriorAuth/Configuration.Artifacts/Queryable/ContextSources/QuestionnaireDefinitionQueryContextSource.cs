using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Configuration.Data;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.ContextSources;

internal sealed class QuestionnaireDefinitionQueryContextSource(
    ConfigurationDbContext dbContext)
    : IQueryContextSource<QuestionnaireDefinitionQueryContext>
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
