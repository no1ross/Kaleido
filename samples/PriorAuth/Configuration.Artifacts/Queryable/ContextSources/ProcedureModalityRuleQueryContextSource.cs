using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data;
using Kaleido.Samples.PriorAuth.Configuration.Artifacts.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Configuration.Artifacts.Queryable.ContextSources;

internal sealed class ProcedureModalityRuleQueryContextSource(
    ConfigurationDbContext dbContext)
    : IQueryContextSource<ProcedureModalityRuleQueryContext>
{
    public IQueryable<ProcedureModalityRuleQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.ProcedureModalityRules
            .AsNoTracking()
            .Select(rule =>
                new ProcedureModalityRuleQueryContext
                {
                    ProcedureModalityRuleId = rule.ProcedureModalityRuleId,
                    CodeSystem = rule.CodeSystem,
                    CodeRangeStart = rule.CodeRangeStart,
                    CodeRangeEnd = rule.CodeRangeEnd,
                    Modality = rule.Modality,
                    Name = rule.Name
                });
    }
}
