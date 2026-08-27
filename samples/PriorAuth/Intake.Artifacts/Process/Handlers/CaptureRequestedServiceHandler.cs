using Kaleido.Process.Participant.Execution;
using Kaleido.Samples.PriorAuth.Configuration.Artifacts;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Data;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Data.Entities;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Messages;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Services;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Handlers;

public sealed class CaptureRequestedServiceHandler(
    IntakeDbContext dbContext,
    ProcedureCodeClient procedureCodeClient,
    ProcedureModalityClient procedureModalityClient)
    : IProcessStepHandler<CaptureRequestedServiceStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureRequestedServiceStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        var procedureCode =
            await procedureCodeClient.GetProcedureCodeAsync(
                processStep.CodeValue,
                processStep.CodeSystem,
                cancellationToken);

        if (procedureCode is null)
        {
            return ProcessStepHandlerResult.Failure(
                IntakeProcessMessages.ProcedureCodeNotFound(
                    processStep.CodeSystem,
                    processStep.CodeValue));
        }

        var priorAuthorization =
            await dbContext.PriorAuthorizations
                .AsNoTracking()
                .SingleAsync(
                    x => x.ProcessId == context.ProcessId,
                    cancellationToken);

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

        var modality =
            await procedureModalityClient.DetermineModalityAsync(
                procedureCode.CodeValue,
                procedureCode.CodeSystem,
                cancellationToken);

        return modality switch
        {
            ProcedureModality.Mri =>
                ProcessStepHandlerResult.Success(
                    requiredStep: nameof(CaptureMriInfoStep).Replace("Step", string.Empty)),
            ProcedureModality.Ct =>
                ProcessStepHandlerResult.Success(
                    requiredStep: nameof(ConfirmCtInsteadOfMriStep).Replace("Step", string.Empty)),
            _ =>
                ProcessStepHandlerResult.Success()
        };
    }
}
