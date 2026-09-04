using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.CodeSet.Data;
using Kaleido.Samples.PriorAuth.CodeSet.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.CodeSet.Queryable.ContextSources;

internal sealed class ProcedureCodeQueryContextSource(
    CodeSetDbContext dbContext)
    : IQueryContextSource<ProcedureCodeQueryContext>
{
    public IQueryable<ProcedureCodeQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.ProcedureCodes
            .AsNoTracking()
            .Select(code =>
                new ProcedureCodeQueryContext
                {
                    ProcedureCodeId = code.ProcedureCodeId,
                    CodeValue = code.CodeValue,
                    CodeSystem = code.CodeSystem,
                    ShortDescription = code.ShortDescription,
                    LongDescription = code.LongDescription,
                    RequiresAuthorization = code.RequiresAuthorization,
                    EffectiveDate = code.EffectiveDate,
                    TerminationDate = code.TerminationDate
                });
    }
}
