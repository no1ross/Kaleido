using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.History.Queryable.Contexts;
using Kaleido.Samples.PriorAuth.History.Queryable.ViewSources.Views;

namespace Kaleido.Samples.PriorAuth.History.Queryable.ViewSources;

[QueryView(
    Name = "prior-auth-records",
    DisplayName = "Prior Auth Records",
    Version = "1.0.0",
    Description = "Prior authorization history records for grid display.",
    DefaultSortField = nameof(PriorAuthRecordQueryContext.LastUpdatedUtc))]
[Pageable(DefaultSize = 25, MaxSize = 100)]
internal sealed class PriorAuthRecordViewSource
    : IQueryViewSource<PriorAuthRecordQueryContext, PriorAuthRecordView>
{
    public IQueryable<PriorAuthRecordView> CreateView(
        IQueryable<PriorAuthRecordQueryContext> query,
        QueryExecutionContext executionContext)
    {
        return query.Select(x =>
            new PriorAuthRecordView
            {
                PriorAuthRecordId = x.PriorAuthRecordId,
                ProcessId = x.ProcessId,
                ProcessorName = x.ProcessorName,
                Status = x.Status,
                MemberNumber = x.MemberNumber,
                MemberDisplayName = x.MemberDisplayName,
                DateOfService = x.DateOfService,
                PrimaryProcedureCode = x.PrimaryProcedureCode,
                PrimaryProcedureDescription = x.PrimaryProcedureDescription,
                CreatedUtc = x.CreatedUtc,
                LastUpdatedUtc = x.LastUpdatedUtc
            });
    }
}
