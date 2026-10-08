using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.CodeSet.Data;
using Kaleido.Samples.PriorAuth.CodeSet.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.CodeSet.Queryable.ContextSources;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Procedure Codes",
    Description = "Procedure codes, searchable by code and description.",
    Source = "Prior Authorization Code Set")]
[Pageable(
    DefaultSize = 25,
    MaxSize = 100)]
internal sealed class ProcedureCodeQueryContextSource(
    CodeSetDbContext dbContext)
    : IQuerySource<ProcedureCodeQueryContext>
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
