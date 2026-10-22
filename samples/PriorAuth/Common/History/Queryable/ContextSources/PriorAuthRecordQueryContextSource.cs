using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.History.Data;
using Kaleido.Samples.PriorAuth.History.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.History.Queryable.ContextSources;

[QuerySource(
    Version = "2.0.0",
    DisplayName = "Prior Auth Records",
    Description = "Prior authorization history: the latest information for each request, one record per process.",
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
                    ProcessId = x.ProcessId,
                    ProcessorName = x.ProcessorName,
                    PriorAuthorizationId = x.PriorAuthorizationId,
                    Status = x.Status,
                    MemberDisplayName = x.MemberDisplayName,
                    MemberNumber = x.MemberNumber,
                    DateOfService = x.DateOfService,
                    PrimaryProcedureCode = x.PrimaryProcedureCode,
                    PrimaryProcedureDescription = x.PrimaryProcedureDescription,
                    CreatedUtc = x.CreatedUtc,
                    UpdatedUtc = x.UpdatedUtc
                });
    }
}
