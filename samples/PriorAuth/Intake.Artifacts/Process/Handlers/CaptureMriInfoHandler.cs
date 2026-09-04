using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.Intake.Data;
using Kaleido.Samples.PriorAuth.Intake.Process.Messages;
using Kaleido.Samples.PriorAuth.Intake.Process.Services;
using Kaleido.Samples.PriorAuth.Intake.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Handlers;

public sealed class CaptureMriInfoHandler(
    IntakeDbContext dbContext,
    MriProcedureCodeResolverClient mriProcedureCodeResolverClient)
    : IProcessStepHandler<CaptureMriInfoStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureMriInfoStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestedService =
                await dbContext.PriorAuthorizationRequestedServices
                    .Join(
                        dbContext.PriorAuthorizations,
                        requestedService => requestedService.PriorAuthorizationId,
                        priorAuthorization => priorAuthorization.PriorAuthorizationId,
                        (requestedService, priorAuthorization) => new { requestedService, priorAuthorization.ProcessId })
                    .Where(x => x.ProcessId == context.ProcessId)
                    .Select(x => x.requestedService)
                    .OrderByDescending(x => x.PriorAuthorizationRequestedServiceId)
                    .FirstAsync(cancellationToken);

            var originalCodeValue = requestedService.ResolvedCodeValue;
            var originalCodeSystem = requestedService.ResolvedCodeSystem;

            var resolvedRule =
                await mriProcedureCodeResolverClient.ResolveAsync(
                    requestedService.UserEnteredCodeValue,
                    requestedService.UserEnteredCodeSystem,
                    processStep,
                    cancellationToken);

            if (resolvedRule is null)
            {
                return ProcessStepHandlerResult.Success();
            }

            requestedService.ResolvedCodeValue = resolvedRule.ResolvedCodeValue;
            requestedService.ResolvedCodeSystem = resolvedRule.ResolvedCodeSystem;
            requestedService.Description = $"MRI {processStep.BodyPart}";

            await dbContext.SaveChangesAsync(cancellationToken);

            if (requestedService.ResolvedCodeValue == originalCodeValue && requestedService.ResolvedCodeSystem == originalCodeSystem)
            {
                return ProcessStepHandlerResult.Success();
            }

            return ProcessStepHandlerResult.Success(
                IntakeProcessMessages.ProcedureCodeUpdated(
                    originalCodeSystem,
                    originalCodeValue,
                    requestedService.ResolvedCodeSystem,
                    requestedService.ResolvedCodeValue));
        }
        catch (QueryableClientException ex)
        {
            return ProcessStepHandlerResult.Failure(
                IntakeProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
    }
}
