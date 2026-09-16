using Kaleido.Process.Http.Client;
using Kaleido.Process.Execution;
using Kaleido.Process;
using Kaleido.Queryable.Http.Client;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.History.Process.Steps;
using Kaleido.Samples.PriorAuth.Intake.Data;
using Kaleido.Samples.PriorAuth.Intake.Data.Entities;
using Kaleido.Samples.PriorAuth.Intake.Process.Messages;
using Kaleido.Samples.PriorAuth.Intake.Process.Services;
using Kaleido.Samples.PriorAuth.Intake.Process.Steps;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Handlers;

public sealed class CaptureRequestedServiceHandler(
    IntakeDbContext dbContext,
    ProcedureCodeClient procedureCodeClient,
    ProductCodeMappingClient productCodeMappingClient,
    IKaleidoProcessClientFactory processClientFactory,
    HistoryClient historyClient)
    : IProcessStepHandler<Intake.Process.Steps.CaptureRequestedServiceStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        Intake.Process.Steps.CaptureRequestedServiceStep processStep,
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
                return ProcessStepHandlerResult.Failure(
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
                return ProcessStepHandlerResult.Failure(
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
                        ProcessId = context.ProcessId,
                        CreatedUtc = DateTimeOffset.UtcNow
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
            session.Procedure.ResolvedProcessorName = processorName;

            await dbContext.SaveChangesAsync(cancellationToken);

            await historyClient.UpsertAsync(
                new UpsertPriorAuthRecordStep
                {
                    ProcessId = context.ProcessId,
                    ProcessorName = "intake",
                    Status = PriorAuthorizationStatus.Draft,
                    PrimaryProcedureCode = session.Procedure.CodeValue,
                    PrimaryProcedureDescription = session.Procedure.ResolvedProcessorName
                },
                cancellationToken);

            // Start the downstream process with whatever member info we have (may be null if member not captured yet)
            var downstreamResult =
                await processClientFactory
                    .GetClient(processorName)
                    .ExecuteStepAsync<StartRadiologyIntakeStep>(
                        new StartRadiologyIntakeStep
                        {
                            MemberId = session.Member?.MemberId,
                            MemberEnrollmentId = session.Member?.MemberEnrollmentId,
                            DateOfService = session.Member?.DateOfService,
                            CodeValue = procedureCode.CodeValue,
                            CodeSystem = procedureCode.CodeSystem
                        },
                        context.ProcessId,
                        cancellationToken);

            if (downstreamResult.Outcome == StepExecutionOutcome.Failed)
            {
                return ProcessStepHandlerResult.Failure(
                    downstreamResult.Messages.ToArray());
            }

            return ProcessStepHandlerResult.HandOff(processorName);
        }
        catch (KaleidoQueryableClientException ex)
        {
            return ProcessStepHandlerResult.Failure(
                IntakeProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
        catch (KaleidoProcessClientException ex)
        {
            return ProcessStepHandlerResult.Failure(
                IntakeProcessMessages.DownstreamProcessorRequestFailed(ex.Message));
        }
    }
}
