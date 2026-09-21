using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Configuration.Data;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.ContextSources;

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
