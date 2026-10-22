using Kaleido.Http.Client;
using Kaleido.Http.Processor;
using Kaleido.Samples.PriorAuth.History.Process;
using Kaleido.Samples.PriorAuth.Intake.Data;
using Kaleido.Samples.PriorAuth.Intake.Data.Entities;
using Kaleido.Samples.PriorAuth.Intake.Process.Messages;
using Kaleido.Samples.PriorAuth.Intake.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Handlers;

public sealed class CaptureRequestedServiceHandler(
    IntakeDbContext dbContext,
    ProcedureCodeClient procedureCodeClient,
    ProductCodeMappingClient productCodeMappingClient,
    IKaleidoProcessorClientFactory processClientFactory,
    HistoryClient historyClient)
    : IProcessStepHandler<Intake.Process.Steps.CaptureRequestedServiceStep>
{
    // The handoff call to the product processor runs inside the transaction too: if it fails,
    // Intake's change and its History snapshot are not kept.
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        Intake.Process.Steps.CaptureRequestedServiceStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken) =>
        HistoryRecording.InTransactionAsync(
            dbContext,
            () => RunAsync(processStep, context, cancellationToken),
            _ => ProcessStepHandlerResult.Reject(IntakeProcessMessages.HistoryNotRecorded(context.ProcessId)),
            cancellationToken);

    private async Task<ProcessStepHandlerResult> RunAsync(
        Intake.Process.Steps.CaptureRequestedServiceStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
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
                return ProcessStepHandlerResult.Reject(
                    IntakeProcessMessages.ProcedureCodeNotFound(
                        processStep.CodeSystem,
                        processStep.CodeValue));
            }

            var processorName =
                await productCodeMappingClient.GetProcessorNameAsync(
                    procedureCode.CodeValue,
                    procedureCode.CodeSystem,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(processorName))
            {
                return ProcessStepHandlerResult.Reject(
                    IntakeProcessMessages.ProcessorNotFoundForCode(
                        procedureCode.CodeSystem,
                        procedureCode.CodeValue));
            }

            var session =
                await dbContext.IntakeSessions
                    .Include(x => x.Member)
                    .Include(x => x.Procedure)
                    .SingleOrDefaultAsync(
                        x => x.ProcessId == context.ProcessId,
                        cancellationToken);

            if (session is null)
            {
                session =
                    new IntakeSession
                    {
                        IntakeSessionId = Guid.NewGuid(),
                        ProcessId = context.ProcessId
                    };

                dbContext.IntakeSessions.Add(session);
            }

            if (session.Procedure is null)
            {
                session.Procedure =
                    new IntakeSessionProcedure
                    {
                        IntakeSessionId = session.IntakeSessionId
                    };
            }

            session.Procedure.CodeValue = procedureCode.CodeValue;
            session.Procedure.CodeSystem = procedureCode.CodeSystem;
            session.Procedure.Description = procedureCode.ShortDescription;
            session.Procedure.ResolvedProcessorName = processorName;

            await dbContext.SaveChangesAsync(cancellationToken);

            await historyClient.RecordAsync(context.ProcessId, cancellationToken);

            // Start the downstream process with whatever member info we have (may be null if member not captured yet)
            var downstreamResult =
                await processClientFactory
                    .GetClient(processorName)
                    .ExecuteStepAsync(
                        new StartRadiologyIntakeStep
                        {
                            MemberId = session.Member?.MemberId,
                            MemberEnrollmentId = session.Member?.MemberEnrollmentId,
                            DateOfService = session.Member?.DateOfService,
                            CodeValue = procedureCode.CodeValue,
                            CodeSystem = procedureCode.CodeSystem,
                            BodyPart = processStep.BodyPart,
                            Laterality = processStep.Laterality,
                            Contrast = processStep.Contrast
                        },
                        cancellationToken);

            if (downstreamResult.Outcome is StepExecutionOutcome.Failed or StepExecutionOutcome.Rejected)
            {
                return ProcessStepHandlerResult.Reject(
                    downstreamResult.BusinessMessages.ToArray());
            }

            return ProcessStepHandlerResult.HandOff(processorName);
        }
        catch (KaleidoHttpClientException ex)
        {
            return ex.Code == HttpClientErrorCodes.ValidationFailed
                ? ProcessStepHandlerResult.Reject(
                    IntakeProcessMessages.QueryableRequestFailed(
                        ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                        ex.Message))
                : ProcessStepHandlerResult.Reject(
                    IntakeProcessMessages.DownstreamProcessorRequestFailed(ex.Message));
        }
    }
}
