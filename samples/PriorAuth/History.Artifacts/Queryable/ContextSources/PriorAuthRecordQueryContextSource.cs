using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.History.Data;
using Kaleido.Samples.PriorAuth.History.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.History.Queryable.ContextSources;

internal sealed class PriorAuthRecordQueryContextSource(
    HistoryDbContext dbContext)
    : IQueryContextSource<PriorAuthRecordQueryContext>
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
