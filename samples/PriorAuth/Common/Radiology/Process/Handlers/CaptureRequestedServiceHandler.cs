using Kaleido.Http.Client;
using Kaleido.Samples.PriorAuth.History.Process.Steps;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Models;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

public sealed class CaptureRequestedServiceHandler(
    RadiologyDbContext dbContext,
    ProcedureCodeClient procedureCodeClient,
    ProcedureModalityClient procedureModalityClient,
    ConfiguredInformationRequests informationRequests,
    HistoryClient historyClient)
    : IProcessStepHandler<CaptureRequestedServiceStep, CaptureRequestedServiceResponse>
{
    public async Task<ProcessStepHandlerResult<CaptureRequestedServiceResponse>> ExecuteAsync(
        CaptureRequestedServiceStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var procedureCode =
                await procedureCodeClient.GetProcedureCodeAsync(
                    processStep.CodeValue,
                    processStep.CodeSystem,
                    cancellationToken);

            if (procedureCode is null)
            {
                return ProcessStepHandlerResult.Reject(RadiologyProcessMessages.ProcedureCodeNotFound(
                        processStep.CodeSystem,
                        processStep.CodeValue));
            }

            var modality =
                await procedureModalityClient.DetermineModalityAsync(
                    procedureCode.CodeValue,
                    procedureCode.CodeSystem,
                    cancellationToken);

            var priorAuthorization =
                await dbContext.PriorAuthorizations
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.ProcessId == context.ProcessId,
                        cancellationToken);

            var existingRequestedServices =
                await dbContext.PriorAuthorizationRequestedServices
                    .AsNoTracking()
                    .Where(x => x.PriorAuthorizationId == priorAuthorization.PriorAuthorizationId)
                    .Select(x => new
                    {
                        x.ResolvedCodeValue,
                        x.ResolvedCodeSystem
                    })
                    .ToListAsync(cancellationToken);

            foreach (var requestedService in existingRequestedServices)
            {
                if (requestedService.ResolvedCodeSystem == procedureCode.CodeSystem
                    && string.Equals(
                        requestedService.ResolvedCodeValue,
                        procedureCode.CodeValue,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessStepHandlerResult.Reject(RadiologyProcessMessages.DuplicateRequestedServiceNotAllowed(
                            procedureCode.CodeSystem,
                            procedureCode.CodeValue));
                }

                var existingModality =
                    await procedureModalityClient.DetermineModalityAsync(
                        requestedService.ResolvedCodeValue,
                        requestedService.ResolvedCodeSystem,
                        cancellationToken);

                if (existingModality != ProcedureModality.Unknown
                    && modality != ProcedureModality.Unknown
                    && existingModality != modality)
                {
                    return ProcessStepHandlerResult.Reject(RadiologyProcessMessages.MixedRequestedServiceModalitiesNotAllowed(
                            existingModality,
                            modality));
                }
            }

            dbContext.PriorAuthorizationRequestedServices.Add(
                new PriorAuthorizationRequestedService
                {
                    PriorAuthorizationRequestedServiceId = Guid.NewGuid(),
                    PriorAuthorizationId = priorAuthorization.PriorAuthorizationId,
                    UserEnteredProcedureCodeId = procedureCode.ProcedureCodeId,
                    UserEnteredCodeValue = processStep.CodeValue,
                    UserEnteredCodeSystem = processStep.CodeSystem,
                    ResolvedProcedureCodeId = procedureCode.ProcedureCodeId,
                    ResolvedCodeValue = procedureCode.CodeValue,
                    ResolvedCodeSystem = procedureCode.CodeSystem,
                    Description = procedureCode.ShortDescription,
                    CreatedUtc = DateTimeOffset.UtcNow,
                    StatedDetailsJson = StatedServiceDetails.ToJson(processStep.BodyPart, processStep.Laterality, processStep.Contrast)
                });

            await dbContext.SaveChangesAsync(cancellationToken);

            await historyClient.UpsertAsync(
                new UpsertPriorAuthRecordStep
                {
                    ProcessorName = "radiology",
                    Status = PriorAuthorizationStatus.Draft,
                    PrimaryProcedureCode = procedureCode.CodeValue,
                    PrimaryProcedureDescription = procedureCode.ShortDescription
                },
                context.ProcessId,
                cancellationToken);

            var response = new CaptureRequestedServiceResponse();

            return modality switch
            {
                ProcedureModality.Mri =>
                    await informationRequests.CreateAsync(context.ProcessId, nameof(CaptureMriInfoStep), modality, procedureCode.CodeValue, cancellationToken) is { } mriRequest
                        ? ProcessStepHandlerResult.Success(response).RequireInformation<CaptureMriInfoStep>(mriRequest)
                        : ProcessStepHandlerResult.Reject(RadiologyProcessMessages.InformationRequestNotConfigured(nameof(CaptureMriInfoStep), modality)),
                ProcedureModality.Ct =>
                    await informationRequests.CreateAsync(context.ProcessId, nameof(ConfirmCtInsteadOfMriStep), modality, procedureCode.CodeValue, cancellationToken) is { } ctRequest
                        ? ProcessStepHandlerResult.Success(response).RequireInformation<ConfirmCtInsteadOfMriStep>(ctRequest)
                        : ProcessStepHandlerResult.Reject(RadiologyProcessMessages.InformationRequestNotConfigured(nameof(ConfirmCtInsteadOfMriStep), modality)),
                _ =>
                    ProcessStepHandlerResult.Success(response)
            };
        }
        catch (KaleidoHttpClientException ex)
        {
            return ProcessStepHandlerResult.Reject(RadiologyProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
    }
}
