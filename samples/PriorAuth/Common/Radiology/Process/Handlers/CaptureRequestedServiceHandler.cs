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
    QuestionnaireDefinitionClient questionnaireDefinitionClient,
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
                return ProcessStepHandlerResult<CaptureRequestedServiceResponse>.Failure(
                    new CaptureRequestedServiceResponse(),
                    RadiologyProcessMessages.ProcedureCodeNotFound(
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
                    return ProcessStepHandlerResult<CaptureRequestedServiceResponse>.Failure(
                        new CaptureRequestedServiceResponse(),
                        RadiologyProcessMessages.DuplicateRequestedServiceNotAllowed(
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
                    return ProcessStepHandlerResult<CaptureRequestedServiceResponse>.Failure(
                        new CaptureRequestedServiceResponse(),
                        RadiologyProcessMessages.MixedRequestedServiceModalitiesNotAllowed(
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
                    Description = procedureCode.ShortDescription
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

            return modality switch
            {
                ProcedureModality.Mri =>
                    await questionnaireDefinitionClient.CreateMriInfoRequestAsync(
                        context.ProcessId,
                        procedureCode.CodeValue,
                        cancellationToken) is { } mriRequest
                        ? ProcessStepHandlerResult<CaptureRequestedServiceResponse>.RequireInformation<CaptureMriInfoStep>(
                            new CaptureRequestedServiceResponse(),
                            mriRequest)
                        : ProcessStepHandlerResult<CaptureRequestedServiceResponse>.Failure(
                            new CaptureRequestedServiceResponse(),
                            RadiologyProcessMessages.InformationRequestNotConfigured(
                                nameof(CaptureMriInfoStep),
                                ProcedureModality.Mri)),
                ProcedureModality.Ct =>
                    ProcessStepHandlerResult<CaptureRequestedServiceResponse>.RequireInformation<ConfirmCtInsteadOfMriStep>(
                        new CaptureRequestedServiceResponse(),
                        questionnaireDefinitionClient.CreateCtConfirmationRequest(
                            procedureCode.CodeValue)),
                _ =>
                    ProcessStepHandlerResult<CaptureRequestedServiceResponse>.Success(
                        new CaptureRequestedServiceResponse())
            };
        }        catch (KaleidoHttpClientException ex)
        {
            return ProcessStepHandlerResult<CaptureRequestedServiceResponse>.Failure(
                new CaptureRequestedServiceResponse(),
                RadiologyProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
    }
}
