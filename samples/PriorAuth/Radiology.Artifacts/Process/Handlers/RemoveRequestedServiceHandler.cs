using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

public sealed class RemoveRequestedServiceHandler(
    IntakeDbContext dbContext)
    : IProcessStepHandler<RemoveRequestedServiceStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        RemoveRequestedServiceStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        var requestedService =
            await dbContext.PriorAuthorizationRequestedServices
                .Join(
                    dbContext.PriorAuthorizations,
                    requestedService => requestedService.PriorAuthorizationId,
                    priorAuthorization => priorAuthorization.PriorAuthorizationId,
                    (requestedService, priorAuthorization) => new
                    {
                        RequestedService = requestedService,
                        priorAuthorization.ProcessId
                    })
                .Where(x =>
                    x.ProcessId == context.ProcessId
                    && x.RequestedService.PriorAuthorizationRequestedServiceId == processStep.PriorAuthorizationRequestedServiceId)
                .Select(x => x.RequestedService)
                .SingleOrDefaultAsync(cancellationToken);

        if (requestedService is null)
        {
            return ProcessStepHandlerResult.Success(
                IntakeProcessMessages.RequestedServiceNotFound(processStep.PriorAuthorizationRequestedServiceId));
        }

        var priorAuthorizationId =
            requestedService.PriorAuthorizationId;

        dbContext.PriorAuthorizationRequestedServices.Remove(requestedService);

        await dbContext.SaveChangesAsync(cancellationToken);

        var hasRemainingRequestedServices =
            await dbContext.PriorAuthorizationRequestedServices
                .AsNoTracking()
                .AnyAsync(
                    x => x.PriorAuthorizationId == priorAuthorizationId,
                    cancellationToken);

        return hasRemainingRequestedServices
            ? ProcessStepHandlerResult.Success()
            : ProcessStepHandlerResult.Success(
                requiredStep: nameof(CaptureRequestedServiceStep).Replace("Step", string.Empty));
    }
}
