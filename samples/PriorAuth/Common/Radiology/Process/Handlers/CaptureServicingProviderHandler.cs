using Kaleido.Http.Client;
using Kaleido.Samples.PriorAuth.History.Process.Steps;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

/// <summary>
/// Records the servicing provider, then starts the clinical review: the clinical question provider's
/// first round (or review, when no clinical review applies).
/// </summary>
public sealed class CaptureServicingProviderHandler(
    RadiologyDbContext dbContext,
    RadiologyClinicalReview clinicalReview,
    HistoryClient historyClient)
    : IProcessStepHandler<CaptureServicingProviderStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureServicingProviderStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var priorAuthorization =
                await dbContext.PriorAuthorizations
                    .Include(x => x.ServicingProvider)
                    .SingleAsync(x => x.ProcessId == context.ProcessId, cancellationToken);

            priorAuthorization.ServicingProvider ??=
                new PriorAuthorizationServicingProvider { PriorAuthorizationId = priorAuthorization.PriorAuthorizationId };

            priorAuthorization.ServicingProvider.ProviderId = processStep.ProviderId;
            priorAuthorization.ServicingProvider.ProviderLocationId = processStep.ProviderLocationId;
            priorAuthorization.ServicingProvider.ProviderName = processStep.ProviderName;
            priorAuthorization.ServicingProvider.LocationName = processStep.LocationName;
            priorAuthorization.ServicingProvider.IsInNetwork = processStep.IsInNetwork;

            await dbContext.SaveChangesAsync(cancellationToken);

            await historyClient.UpsertAsync(
                new UpsertPriorAuthRecordStep { ProcessorName = "radiology", Status = PriorAuthorizationStatus.Draft },
                context.ProcessId,
                cancellationToken);

            return await clinicalReview.StartAsync(context.ProcessId, cancellationToken) is { } clinical
                ? ProcessStepHandlerResult.RequireInformation<CaptureClinicalInfoStep>(clinical)
                : ProcessStepHandlerResult.Success<ReviewPriorAuthorizationStep>();
        }
        catch (KaleidoHttpClientException ex)
        {
            return ProcessStepHandlerResult.Failure(
                RadiologyProcessMessages.QueryableRequestFailed(ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED", ex.Message));
        }
    }
}