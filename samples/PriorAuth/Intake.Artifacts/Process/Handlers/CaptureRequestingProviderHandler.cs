using Kaleido.Process.Participant.Execution;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Data;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Data.Entities;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Handlers;

public sealed class CaptureRequestingProviderHandler(
    IntakeDbContext dbContext)
    : IProcessStepHandler<CaptureRequestingProviderStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureRequestingProviderStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        var priorAuthorization =
            await dbContext.PriorAuthorizations
                .Include(x => x.RequestingProvider)
                .SingleAsync(
                    x => x.ProcessId == context.ProcessId,
                    cancellationToken);

        if (priorAuthorization.RequestingProvider is null)
        {
            priorAuthorization.RequestingProvider =
                new PriorAuthorizationRequestingProvider
                {
                    PriorAuthorizationId = priorAuthorization.PriorAuthorizationId
                };
        }

        priorAuthorization.RequestingProvider.ProviderId = processStep.ProviderId;
        priorAuthorization.RequestingProvider.ProviderLocationId = processStep.ProviderLocationId;
        priorAuthorization.RequestingProvider.ProviderName = processStep.ProviderName;
        priorAuthorization.RequestingProvider.LocationName = processStep.LocationName;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ProcessStepHandlerResult.Success(
            requiredStep: nameof(CaptureServicingProviderStep).Replace("Step", string.Empty));
    }
}
