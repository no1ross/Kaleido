using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.Configuration;
using Kaleido.Samples.PriorAuth.Intake.Data.Entities;
using Kaleido.Samples.PriorAuth.Intake.Data;
using Kaleido.Samples.PriorAuth.Intake.Process.Messages;
using Kaleido.Samples.PriorAuth.Intake.Process.Models;
using Kaleido.Samples.PriorAuth.Intake.Process.Services;
using Kaleido.Samples.PriorAuth.Intake.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Handlers;

public sealed class CaptureRequestedServiceHandler(
    IntakeDbContext dbContext,
    ProcedureCodeClient procedureCodeClient,
    ProcedureModalityClient procedureModalityClient,
    QuestionnaireDefinitionClient questionnaireDefinitionClient)
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
                    IntakeProcessMessages.ProcedureCodeNotFound(
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
                        IntakeProcessMessages.DuplicateRequestedServiceNotAllowed(
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
                        IntakeProcessMessages.MixedRequestedServiceModalitiesNotAllowed(
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

        return modality switch
        {
            ProcedureModality.Mri =>
                await CreateMriResponseAsync(
                    context.ProcessId,
                    procedureCode.CodeValue,
                    cancellationToken),
            ProcedureModality.Ct =>
                await CreateCtResponseAsync(
                    context.ProcessId,
                    procedureCode.CodeValue,
                    cancellationToken),
            _ =>
                ProcessStepHandlerResult<CaptureRequestedServiceResponse>.Success(
                    new CaptureRequestedServiceResponse())
        };

            async Task<ProcessStepHandlerResult<CaptureRequestedServiceResponse>> CreateMriResponseAsync(
                Guid processId,
                string procedureCodeValue,
                CancellationToken ct)
            {
                var response =
                    await questionnaireDefinitionClient.ResolveAsync(
                        processId,
                        nameof(CaptureMriInfoStep).Replace("Step", string.Empty),
                        ProcedureModality.Mri,
                        procedureCodeValue,
                        ct)
                    ?? new CaptureRequestedServiceResponse();

                return ProcessStepHandlerResult<CaptureRequestedServiceResponse>.Success(
                    response,
                    requiredStep: nameof(CaptureMriInfoStep).Replace("Step", string.Empty));
            }

            async Task<ProcessStepHandlerResult<CaptureRequestedServiceResponse>> CreateCtResponseAsync(
                Guid processId,
                string procedureCodeValue,
                CancellationToken ct)
            {
                var response =
                    await questionnaireDefinitionClient.ResolveAsync(
                        processId,
                        nameof(CaptureMriInfoStep).Replace("Step", string.Empty),
                        ProcedureModality.Mri,
                        procedureCodeValue,
                        ct)
                    ?? new CaptureRequestedServiceResponse();

                return ProcessStepHandlerResult<CaptureRequestedServiceResponse>.Success(
                    response,
                    requiredStep: nameof(ConfirmCtInsteadOfMriStep).Replace("Step", string.Empty));
            }
        }
        catch (QueryableClientException ex)
        {
            return ProcessStepHandlerResult<CaptureRequestedServiceResponse>.Failure(
                new CaptureRequestedServiceResponse(),
                IntakeProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
    }
}
