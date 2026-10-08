using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Queryable.ContextSources;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Requested Services",
    Description = "Services requested on a radiology prior authorization.",
    Source = "Prior Authorization Radiology")]
internal sealed class RequestedServiceQueryContextSource(
    RadiologyDbContext dbContext)
    : IQuerySource<RequestedServiceQueryContext>
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
