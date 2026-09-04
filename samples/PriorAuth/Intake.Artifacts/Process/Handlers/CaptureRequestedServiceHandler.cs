using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.Configuration;
using Kaleido.Samples.PriorAuth.Intake.Data;
using Kaleido.Samples.PriorAuth.Intake.Data.Entities;
using Kaleido.Samples.PriorAuth.Intake.Process.Messages;
using Kaleido.Samples.PriorAuth.Intake.Process.Models;
using Kaleido.Samples.PriorAuth.Intake.Process.Services;
using Kaleido.Samples.PriorAuth.Intake.Process.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Handlers;

public sealed class CaptureRequestedServiceHandler(
    IntakeDbContext dbContext,
    ProcedureCodeClient procedureCodeClient,
    ProcedureModalityClient procedureModalityClient,
    IConfiguration configuration)
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

            var processorName =
                configuration[$"ProcessorMappings:{modality}"];

            if (string.IsNullOrWhiteSpace(processorName))
            {
                return ProcessStepHandlerResult<CaptureRequestedServiceResponse>.Failure(
                    new CaptureRequestedServiceResponse(),
                    IntakeProcessMessages.ProcessorNotFound(modality));
            }

            var session =
                await dbContext.IntakeSessions
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

            return ProcessStepHandlerResult<CaptureRequestedServiceResponse>.Success(
                new CaptureRequestedServiceResponse
                {
                    ProcessorName = processorName
                });
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
