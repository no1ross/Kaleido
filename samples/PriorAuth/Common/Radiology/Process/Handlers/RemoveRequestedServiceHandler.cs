using Kaleido.Samples.PriorAuth.History.Process;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

/// <summary>
/// Removes a requested service. The primary procedure History shows may change with it, so the
/// removal and the History snapshot are kept together.
/// </summary>
public sealed class RemoveRequestedServiceHandler(
    RadiologyDbContext dbContext,
    HistoryClient historyClient)
    : IProcessStepHandler<RemoveRequestedServiceStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        RemoveRequestedServiceStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken) =>
        HistoryRecording.InTransactionAsync(
            dbContext,
            () => RunAsync(processStep, context, cancellationToken),
            _ => ProcessStepHandlerResult.Reject(RadiologyProcessMessages.HistoryNotRecorded(context.ProcessId)),
            cancellationToken);

    private async Task<ProcessStepHandlerResult> RunAsync(
        RemoveRequestedServiceStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
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
                RadiologyProcessMessages.RequestedServiceNotFound(processStep.PriorAuthorizationRequestedServiceId));
        }

        var priorAuthorizationId =
            requestedService.PriorAuthorizationId;

        dbContext.PriorAuthorizationRequestedServices.Remove(requestedService);

        await dbContext.SaveChangesAsync(cancellationToken);

        await historyClient.RecordAsync(context.ProcessId, cancellationToken);

        var hasRemainingRequestedServices =
            await dbContext.PriorAuthorizationRequestedServices
                .AsNoTracking()
                .AnyAsync(
                    x => x.PriorAuthorizationId == priorAuthorizationId,
                    cancellationToken);

        return hasRemainingRequestedServices
            ? ProcessStepHandlerResult.Success()
            : ProcessStepHandlerResult.Success<CaptureRequestedServiceStep>();
    }
}
