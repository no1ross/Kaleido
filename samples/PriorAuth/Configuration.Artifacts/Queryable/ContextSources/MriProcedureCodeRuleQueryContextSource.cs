using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data;
using Kaleido.Samples.PriorAuth.Configuration.Artifacts.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Configuration.Artifacts.Queryable.ContextSources;

internal sealed class MriProcedureCodeRuleQueryContextSource(
    ConfigurationDbContext dbContext)
    : IQueryContextSource<MriProcedureCodeRuleQueryContext>
{
    public IQueryable<MriProcedureCodeRuleQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.MriProcedureCodeRules
            .AsNoTracking()
            .Select(rule =>
                new MriProcedureCodeRuleQueryContext
                {
                    MriProcedureCodeRuleId = rule.MriProcedureCodeRuleId,
                    SelectedCodeSystem = rule.SelectedCodeSystem,
                    SelectedCodeValue = rule.SelectedCodeValue,
                    Modality = ProcedureModality.Mri,
                    BodyPart = rule.BodyPart,
                    Laterality = rule.Laterality,
                    Contrast = rule.Contrast,
                    ResolvedCodeSystem = rule.ResolvedCodeSystem,
                    ResolvedCodeValue = rule.ResolvedCodeValue
                });
    }
}
