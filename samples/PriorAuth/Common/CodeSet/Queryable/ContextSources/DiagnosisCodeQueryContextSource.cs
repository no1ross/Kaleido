using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.CodeSet.Data;
using Kaleido.Samples.PriorAuth.CodeSet.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.CodeSet.Queryable.ContextSources;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Diagnosis Codes",
    Description = "Diagnosis codes, searchable by code and description.",
    Source = "Prior Authorization Code Set")]
[Pageable(
    DefaultSize = 25,
    MaxSize = 100)]
internal sealed class DiagnosisCodeQueryContextSource(
    CodeSetDbContext dbContext)
    : IQuerySource<DiagnosisCodeQueryContext>
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
