using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Queryable.ContextSources;

internal sealed class RequestedServiceQueryContextSource(
    IntakeDbContext dbContext)
    : IQueryContextSource<RequestedServiceQueryContext>
{
    public IQueryable<RequestedServiceQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.PriorAuthorizationRequestedServices
            .AsNoTracking()
            .Join(
                dbContext.PriorAuthorizations.AsNoTracking(),
                requestedService => requestedService.PriorAuthorizationId,
                priorAuthorization => priorAuthorization.PriorAuthorizationId,
                (requestedService, priorAuthorization) => new RequestedServiceQueryContext
                {
                    PriorAuthorizationRequestedServiceId = requestedService.PriorAuthorizationRequestedServiceId,
                    PriorAuthorizationId = requestedService.PriorAuthorizationId,
                    ProcessId = priorAuthorization.ProcessId,
                    UserEnteredCodeValue = requestedService.UserEnteredCodeValue,
                    UserEnteredCodeSystem = requestedService.UserEnteredCodeSystem,
                    ResolvedCodeValue = requestedService.ResolvedCodeValue,
                    ResolvedCodeSystem = requestedService.ResolvedCodeSystem,
                    Description = requestedService.Description
                });
    }
}
