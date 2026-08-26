using Kaleido.Process.Participant.Execution;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Data;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Data.Entities;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Handlers;

public sealed class CaptureRequestedServiceHandler(
    IntakeDbContext dbContext)
    : IProcessStepHandler<CaptureRequestedServiceStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureRequestedServiceStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
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
                ProcedureCodeId = processStep.ProcedureCodeId,
                CodeValue = processStep.CodeValue,
                CodeSystem = processStep.CodeSystem,
                Description = processStep.Description
            });

        await dbContext.SaveChangesAsync(cancellationToken);

        return ProcessStepHandlerResult.Success();
    }
}
