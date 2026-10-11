using Kaleido.Samples.PriorAuth.History.Process.Steps;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

/// <summary>
/// Submits the request when the caller attests. Records the submission; approval is decided
/// elsewhere, never by the process engine.
/// </summary>
public sealed class SubmitPriorAuthorizationHandler(
    RadiologyDbContext dbContext,
    ConfiguredInformationRequests informationRequests,
    HistoryClient historyClient)
    : IProcessStepHandler<SubmitPriorAuthorizationStep>
{
    // The binding key the business configures on the submission attestation.
    private const string AttestedItem = "Attested";

    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        SubmitPriorAuthorizationStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        await informationRequests.RecordAnswersAsync(context.ProcessId, nameof(SubmitPriorAuthorizationStep), processStep, cancellationToken);

        if (!bool.TryParse(ConfiguredInformationRequests.Answer(processStep, AttestedItem), out var attested) || !attested)
        {
            return ProcessStepHandlerResult.Reject(RadiologyProcessMessages.AttestationRequired());
        }

        var priorAuthorization =
            await dbContext.PriorAuthorizations
                .Include(x => x.Member)
                .Include(x => x.RequestedServices)
                .SingleAsync(x => x.ProcessId == context.ProcessId, cancellationToken);

        priorAuthorization.Status = PriorAuthorizationStatus.Submitted;
        priorAuthorization.SubmittedUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var primaryService = priorAuthorization.RequestedServices.MinBy(x => x.CreatedUtc);

        await historyClient.UpsertAsync(
            new UpsertPriorAuthRecordStep
            {
                ProcessorName = "radiology",
                Status = PriorAuthorizationStatus.Submitted,
                MemberNumber = priorAuthorization.Member?.MemberNumber ?? string.Empty,
                MemberDisplayName = priorAuthorization.Member?.DisplayName ?? string.Empty,
                PrimaryProcedureCode = primaryService?.ResolvedCodeValue ?? string.Empty,
                PrimaryProcedureDescription = primaryService?.Description ?? string.Empty
            },
            context.ProcessId,
            cancellationToken);

        return ProcessStepHandlerResult.Success(RadiologyProcessMessages.Submitted(context.ProcessId));
    }
}
