using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

public sealed class CaptureRequestingProviderHandler(
    RadiologyDbContext dbContext)
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
