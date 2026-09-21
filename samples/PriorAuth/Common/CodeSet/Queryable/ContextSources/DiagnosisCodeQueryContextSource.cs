using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.CodeSet.Data;
using Kaleido.Samples.PriorAuth.CodeSet.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.CodeSet.Queryable.ContextSources;

internal sealed class DiagnosisCodeQueryContextSource(
    CodeSetDbContext dbContext)
    : IQueryContextSource<DiagnosisCodeQueryContext>
{
    public IQueryable<DiagnosisCodeQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.DiagnosisCodes
            .AsNoTracking()
            .Select(code =>
                new DiagnosisCodeQueryContext
                {
                    DiagnosisCodeId = code.DiagnosisCodeId,
                    CodeValue = code.CodeValue,
                    CodeSystem = code.CodeSystem,
                    ShortDescription = code.ShortDescription,
                    LongDescription = code.LongDescription,
                    EffectiveDate = code.EffectiveDate,
                    TerminationDate = code.TerminationDate
                });
    }
}
