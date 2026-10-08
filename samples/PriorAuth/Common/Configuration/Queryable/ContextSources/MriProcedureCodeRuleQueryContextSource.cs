using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.Configuration.Data;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.ContextSources;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "MRI Procedure Code Rules",
    Description = "Rules that resolve the MRI procedure code to use for a requested service.",
    Source = "Prior Authorization Configuration")]
internal sealed class MriProcedureCodeRuleQueryContextSource(
    ConfigurationDbContext dbContext)
    : IQuerySource<MriProcedureCodeRuleQueryContext>
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
