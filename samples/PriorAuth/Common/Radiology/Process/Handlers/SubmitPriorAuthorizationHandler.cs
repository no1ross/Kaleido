using Kaleido.Samples.PriorAuth.History.Process;
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

    public Task<ProcessStepHandlerResult> ExecuteAsync(
        SubmitPriorAuthorizationStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken) =>
        HistoryRecording.InTransactionAsync(
            dbContext,
            () => RunAsync(processStep, context, cancellationToken),
            _ => ProcessStepHandlerResult.Reject(RadiologyProcessMessages.HistoryNotRecorded(context.ProcessId)),
            cancellationToken);

    private async Task<ProcessStepHandlerResult> RunAsync(
        SubmitPriorAuthorizationStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        await informationRequests.RecordAnswersAsync(context.ProcessId, nameof(SubmitPriorAuthorizationStep), processStep, cancellationToken);

        if (!bool.TryParse(ConfiguredInformationRequests.Answer(processStep, AttestedItem), out var attested) || !attested)
        {
            return ProcessStepHandlerResult.Reject(RadiologyProcessMessages.AttestationRequired());
        }

        var priorAuthorization =
            await dbContext.PriorAuthorizations
                .SingleAsync(x => x.ProcessId == context.ProcessId, cancellationToken);

        priorAuthorization.Status = PriorAuthorizationStatus.Submitted;
        priorAuthorization.SubmittedUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        await historyClient.RecordAsync(context.ProcessId, cancellationToken);

        return ProcessStepHandlerResult.Success(RadiologyProcessMessages.Submitted(context.ProcessId));
    }
}
