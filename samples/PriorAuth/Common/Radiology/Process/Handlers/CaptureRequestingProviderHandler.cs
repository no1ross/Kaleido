using Kaleido.Http.Client;
using Kaleido.Samples.PriorAuth.History.Process;
using Kaleido.Samples.PriorAuth.Provider.Queryable.Parameters;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

/// <summary>
/// Records the requesting provider from the search result the caller chose: the location must
/// belong to the provider and be offered for this request (rejected otherwise); names and network
/// status come from that result.
/// </summary>
public sealed class CaptureRequestingProviderHandler(
    RadiologyDbContext dbContext,
    RequestingProviderSearchClient providerSearch,
    HistoryClient historyClient)
    : IProcessStepHandler<CaptureRequestingProviderStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureRequestingProviderStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default) =>
        HistoryRecording.InTransactionAsync(
            dbContext,
            () => RunAsync(processStep, context, cancellationToken),
            _ => ProcessStepHandlerResult.Reject(RadiologyProcessMessages.HistoryNotRecorded(context.ProcessId)),
            cancellationToken);

    private async Task<ProcessStepHandlerResult> RunAsync(
        CaptureRequestingProviderStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var chosen = await providerSearch.FindAsync(context.ProcessId, processStep.ProviderLocationId, ProviderType.RequestingProvider, cancellationToken);

            if (chosen is null || chosen.ProviderId != processStep.ProviderId)
            {
                return ProcessStepHandlerResult.Reject(
                    RadiologyProcessMessages.ProviderLocationNotOffered(processStep.ProviderId, processStep.ProviderLocationId));
            }

            var priorAuthorization =
                await dbContext.PriorAuthorizations
                    .Include(x => x.RequestingProvider)
                    .SingleAsync(x => x.ProcessId == context.ProcessId, cancellationToken);

            priorAuthorization.RequestingProvider ??=
                new PriorAuthorizationRequestingProvider { PriorAuthorizationId = priorAuthorization.PriorAuthorizationId };

            priorAuthorization.RequestingProvider.ProviderId = chosen.ProviderId;
            priorAuthorization.RequestingProvider.ProviderLocationId = chosen.ProviderLocationId;
            priorAuthorization.RequestingProvider.ProviderName = chosen.ProviderName;
            priorAuthorization.RequestingProvider.LocationName = chosen.LocationName;
            priorAuthorization.RequestingProvider.IsInNetwork = chosen.IsInNetwork;

            await dbContext.SaveChangesAsync(cancellationToken);

            await historyClient.RecordAsync(context.ProcessId, cancellationToken);

            return ProcessStepHandlerResult.Success<CaptureServicingProviderStep>();
        }
        catch (KaleidoHttpClientException ex)
        {
            return ProcessStepHandlerResult.Reject(
                RadiologyProcessMessages.QueryableRequestFailed(ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED", ex.Message));
        }
    }
}
