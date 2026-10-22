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
/// Records the servicing provider from the search result the caller chose (the location must belong
/// to the facility and be offered for this request; names and network status come from that result),
/// then starts the clinical review: the clinical question provider's
/// first round (or review, when no clinical review applies).
/// </summary>
public sealed class CaptureServicingProviderHandler(
    RadiologyDbContext dbContext,
    RequestingProviderSearchClient providerSearch,
    RadiologyClinicalReview clinicalReview,
    HistoryClient historyClient)
    : IProcessStepHandler<CaptureServicingProviderStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureServicingProviderStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken) =>
        HistoryRecording.InTransactionAsync(
            dbContext,
            () => RunAsync(processStep, context, cancellationToken),
            _ => ProcessStepHandlerResult.Reject(RadiologyProcessMessages.HistoryNotRecorded(context.ProcessId)),
            cancellationToken);

    private async Task<ProcessStepHandlerResult> RunAsync(
        CaptureServicingProviderStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var chosen = await providerSearch.FindAsync(context.ProcessId, processStep.ProviderLocationId, ProviderType.ServicingFacility, cancellationToken);

            if (chosen is null || chosen.ProviderId != processStep.ProviderId)
            {
                return ProcessStepHandlerResult.Reject(
                    RadiologyProcessMessages.ProviderLocationNotOffered(processStep.ProviderId, processStep.ProviderLocationId));
            }

            var priorAuthorization =
                await dbContext.PriorAuthorizations
                    .Include(x => x.ServicingProvider)
                    .SingleAsync(x => x.ProcessId == context.ProcessId, cancellationToken);

            priorAuthorization.ServicingProvider ??=
                new PriorAuthorizationServicingProvider { PriorAuthorizationId = priorAuthorization.PriorAuthorizationId };

            priorAuthorization.ServicingProvider.ProviderId = chosen.ProviderId;
            priorAuthorization.ServicingProvider.ProviderLocationId = chosen.ProviderLocationId;
            priorAuthorization.ServicingProvider.ProviderName = chosen.ProviderName;
            priorAuthorization.ServicingProvider.LocationName = chosen.LocationName;
            priorAuthorization.ServicingProvider.IsInNetwork = chosen.IsInNetwork;

            await dbContext.SaveChangesAsync(cancellationToken);

            await historyClient.RecordAsync(context.ProcessId, cancellationToken);

            return await clinicalReview.StartAsync(context.ProcessId, cancellationToken) is { } clinical
                ? ProcessStepHandlerResult.RequireInformation<CaptureClinicalInfoStep>(clinical)
                : ProcessStepHandlerResult.Success<ReviewPriorAuthorizationStep>();
        }
        catch (KaleidoHttpClientException ex)
        {
            return ProcessStepHandlerResult.Reject(
                RadiologyProcessMessages.QueryableRequestFailed(ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED", ex.Message));
        }
    }
}
