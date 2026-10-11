using Kaleido.Http.Client;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Models;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

/// <summary>
/// Returns the whole request for read-back and presents the attestation configured for submission.
/// </summary>
public sealed class ReviewPriorAuthorizationHandler(
    RadiologyDbContext dbContext,
    ProcedureModalityClient procedureModalityClient,
    ConfiguredInformationRequests informationRequests)
    : IProcessStepHandler<ReviewPriorAuthorizationStep, PriorAuthorizationReview>
{
    public async Task<ProcessStepHandlerResult<PriorAuthorizationReview>> ExecuteAsync(
        ReviewPriorAuthorizationStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var priorAuthorization =
                await dbContext.PriorAuthorizations
                    .AsNoTracking()
                    .Include(x => x.Member)
                    .Include(x => x.RequestedServices)
                    .Include(x => x.RequestingProvider)
                    .Include(x => x.ServicingProvider)
                    .SingleAsync(x => x.ProcessId == context.ProcessId, cancellationToken);

            var services = priorAuthorization.RequestedServices.OrderBy(x => x.CreatedUtc).ToArray();

            var review = new PriorAuthorizationReview
            {
                Status = priorAuthorization.Status,
                Member = priorAuthorization.Member is { } member
                    ? new ReviewedMember(member.DisplayName, member.MemberNumber, member.PlanName)
                    : null,
                RequestedServices = services
                    .Select(x => new ReviewedService(x.ResolvedCodeValue, x.ResolvedCodeSystem, x.Description))
                    .ToArray(),
                RequestingProvider = priorAuthorization.RequestingProvider is { } requesting
                    ? new ReviewedProvider(requesting.ProviderName, requesting.LocationName, requesting.IsInNetwork)
                    : null,
                ServicingProvider = priorAuthorization.ServicingProvider is { } servicing
                    ? new ReviewedProvider(servicing.ProviderName, servicing.LocationName, servicing.IsInNetwork)
                    : null,
                ClinicalDetermination = priorAuthorization.ClinicalDetermination,
                Answers = await informationRequests.AnswersAsync(context.ProcessId, cancellationToken)
            };

            var primaryService = services.FirstOrDefault();

            var modality = primaryService is null
                ? ProcedureModality.Unknown
                : await procedureModalityClient.DetermineModalityAsync(primaryService.ResolvedCodeValue, primaryService.ResolvedCodeSystem, cancellationToken);

            return await informationRequests.CreateAsync(
                    context.ProcessId,
                    nameof(SubmitPriorAuthorizationStep),
                    modality,
                    primaryService?.ResolvedCodeValue,
                    cancellationToken) is { } attestation
                ? ProcessStepHandlerResult.Success(review).RequireInformation<SubmitPriorAuthorizationStep>(attestation)
                : ProcessStepHandlerResult.Reject(RadiologyProcessMessages.InformationRequestNotConfigured(nameof(SubmitPriorAuthorizationStep), modality));
        }
        catch (KaleidoHttpClientException ex)
        {
            return ProcessStepHandlerResult.Reject(RadiologyProcessMessages.QueryableRequestFailed(ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED", ex.Message));
        }
    }
}
