using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.History.Data;
using Kaleido.Samples.PriorAuth.History.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.History.Queryable.ContextSources;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Prior Auth Records",
    Description = "Prior authorization history records.",
    Source = "Prior Auth History Service")]
internal sealed class PriorAuthRecordQueryContextSource(
    HistoryDbContext dbContext)
    : IQuerySource<PriorAuthRecordQueryContext>
{
    public IQueryable<PriorAuthRecordQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.PriorAuthRecords
            .AsNoTracking()
            .Select(x =>
                new PriorAuthRecordQueryContext
                {
                    PriorAuthRecordId = x.PriorAuthRecordId,
                    ProcessId = x.ProcessId,
                    ProcessorName = x.ProcessorName,
                    Status = x.Status,
                    MemberDisplayName = x.MemberDisplayName,
                    MemberNumber = x.MemberNumber,
                    DateOfService = x.DateOfService,
                    PrimaryProcedureCode = x.PrimaryProcedureCode,
                    PrimaryProcedureDescription = x.PrimaryProcedureDescription,
                    CreatedUtc = x.CreatedUtc,
                    LastUpdatedUtc = x.LastUpdatedUtc
                });
    }
}
