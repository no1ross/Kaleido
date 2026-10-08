using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.Configuration.Data;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.ContextSources;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Procedure Modality Rules",
    Description = "Rules that map procedure codes to an imaging modality (for example MRI or CT).",
    Source = "Prior Authorization Configuration")]
internal sealed class ProcedureModalityRuleQueryContextSource(
    ConfigurationDbContext dbContext)
    : IQuerySource<ProcedureModalityRuleQueryContext>
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
