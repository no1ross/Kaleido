using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.History.Queryable.Contexts;
using Kaleido.Samples.PriorAuth.History.Queryable.ContextSources;
using Kaleido.Samples.PriorAuth.History.Queryable.ViewSources.Views;

namespace Kaleido.Samples.PriorAuth.History.Queryable.ViewSources;

[QueryView(
    DisplayName = "Prior Auth Record List",
    Version = "2.0.0",
    Description = "Prior authorization history records for grid display.",
    DefaultSortField = nameof(PriorAuthRecordQueryContext.LastUpdatedUtc))]
[Pageable(DefaultSize = 25, MaxSize = 100)]
internal sealed class PriorAuthRecordListViewSource
    : IQueryViewSource<PriorAuthRecordQueryContextSource, PriorAuthRecordQueryContext, PriorAuthRecordView>
{
    public IQueryable<PriorAuthRecordView> CreateView(
        IQueryable<PriorAuthRecordQueryContext> query,
        QueryExecutionContext executionContext)
    {
        return query.Select(x =>
            new PriorAuthRecordView
            {
                ProcessId = x.ProcessId,
                ProcessorName = x.ProcessorName,
                PriorAuthorizationId = x.PriorAuthorizationId,
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
