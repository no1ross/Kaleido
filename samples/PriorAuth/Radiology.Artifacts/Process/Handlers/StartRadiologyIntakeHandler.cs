using Kaleido.Process;
using Kaleido.Process.Execution;
using Kaleido.Queryable.AspNetCore.Client;
using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Kaleido.Samples.PriorAuth.Radiology;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Models;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.History.Process.Steps;
using Kaleido.Samples.PriorAuth.Configuration;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

/// <summary>
/// Handles the <see cref="StartRadiologyIntakeStep"/> submitted by the Intake processor
/// during a cross-processor handoff. Combines the member validation and requested service
/// capture into a single atomic step so the consumer only interacts with Intake.
/// </summary>
public sealed class StartRadiologyIntakeHandler(
    RadiologyDbContext dbContext,
    MemberDetailsClient memberDetailsClient,
    ProcedureCodeClient procedureCodeClient,
    ProcedureModalityClient procedureModalityClient,
    QuestionnaireDefinitionClient questionnaireDefinitionClient,
    HistoryClient historyClient)
    : IProcessStepHandler<StartRadiologyIntakeStep, StartRadiologyIntakeResponse>
{
    public async Task<ProcessStepHandlerResult<StartRadiologyIntakeResponse>> ExecuteAsync(
        StartRadiologyIntakeStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // --- Member validation (if member info provided) ---

            if (processStep.MemberId.HasValue && processStep.MemberEnrollmentId.HasValue)
            {
                var memberDetails =
                    await memberDetailsClient.GetMemberDetailsAsync(
                        processStep.MemberId.Value,
                        processStep.MemberEnrollmentId.Value,
                        cancellationToken);

                if (memberDetails is null)
                {
                    return ProcessStepHandlerResult<StartRadiologyIntakeResponse>.Failure(
                        new StartRadiologyIntakeResponse(),
                        RadiologyProcessMessages.MemberNotFound(
                            processStep.MemberId.Value,
                            processStep.MemberEnrollmentId.Value));
                }

                if (processStep.DateOfService.HasValue && processStep.DateOfService < memberDetails.EffectiveDate)
                {
                    return ProcessStepHandlerResult<StartRadiologyIntakeResponse>.Failure(
                        new StartRadiologyIntakeResponse(),
                        RadiologyProcessMessages.CoverageNotYetEffective(
                            processStep.MemberEnrollmentId.Value,
                            processStep.DateOfService.Value,
                            memberDetails.EffectiveDate));
                }

                if (memberDetails.TerminationDate is DateOnly terminationDate
                    && processStep.DateOfService.HasValue && processStep.DateOfService > terminationDate)
                {
                    return ProcessStepHandlerResult<StartRadiologyIntakeResponse>.Failure(
                        new StartRadiologyIntakeResponse(),
                        RadiologyProcessMessages.CoverageTerminated(
                            processStep.MemberEnrollmentId.Value,
                            processStep.DateOfService.Value,
                            terminationDate));
                }
            }

            // --- Procedure code resolution ---

            var procedureCode =
                await procedureCodeClient.GetProcedureCodeAsync(
                    processStep.CodeValue,
                    processStep.CodeSystem,
                    cancellationToken);

            if (procedureCode is null)
            {
                return ProcessStepHandlerResult<StartRadiologyIntakeResponse>.Failure(
                    new StartRadiologyIntakeResponse(),
                    RadiologyProcessMessages.ProcedureCodeNotFound(
                        processStep.CodeSystem,
                        processStep.CodeValue));
            }

            var modality =
                await procedureModalityClient.DetermineModalityAsync(
                    procedureCode.CodeValue,
                    procedureCode.CodeSystem,
                    cancellationToken);

            // --- Upsert PriorAuthorization + Member (if member info provided) ---

            var priorAuthorization =
                await dbContext.PriorAuthorizations
                    .Include(x => x.Member)
                    .SingleOrDefaultAsync(
                        x => x.ProcessId == context.ProcessId,
                        cancellationToken);

            if (priorAuthorization is null)
            {
                priorAuthorization =
                    new PriorAuthorization
                    {
                        PriorAuthorizationId = Guid.NewGuid(),
                        ProcessId = context.ProcessId,
                        Status = PriorAuthorizationStatus.Draft,
                        CreatedUtc = DateTimeOffset.UtcNow
                    };

                dbContext.PriorAuthorizations.Add(priorAuthorization);
            }

            if (processStep.MemberId.HasValue && processStep.MemberEnrollmentId.HasValue)
            {
                if (priorAuthorization.Member is null)
                {
                    priorAuthorization.Member =
                        new PriorAuthorizationMember
                        {
                            PriorAuthorizationId = priorAuthorization.PriorAuthorizationId
                        };
                }

                // Re-fetch member details if we have them
                var memberDetails =
                    await memberDetailsClient.GetMemberDetailsAsync(
                        processStep.MemberId.Value,
                        processStep.MemberEnrollmentId.Value,
                        cancellationToken);

                if (memberDetails is not null)
                {
                    priorAuthorization.Member.MemberId = memberDetails.MemberId;
                    priorAuthorization.Member.MemberEnrollmentId = memberDetails.MemberEnrollmentId;
                    priorAuthorization.Member.MemberNumber = memberDetails.MemberNumber;
                    priorAuthorization.Member.DisplayName = memberDetails.DisplayName;
                    priorAuthorization.Member.PlanId = memberDetails.PlanId;
                    priorAuthorization.Member.PlanName = memberDetails.PlanName;
                    priorAuthorization.Member.LineOfBusiness = memberDetails.LineOfBusiness;
                }
            }

            // --- Add requested service ---

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

            // --- History ---

            await historyClient.UpsertAsync(
                new UpsertPriorAuthRecordStep
                {
                    ProcessId = context.ProcessId,
                    ProcessorName = "radiology",
                    Status = PriorAuthorizationStatus.Draft,
                    MemberNumber = priorAuthorization.Member?.MemberNumber ?? string.Empty,
                    MemberDisplayName = priorAuthorization.Member?.DisplayName ?? string.Empty,
                    DateOfService = processStep.DateOfService ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    PrimaryProcedureCode = procedureCode.CodeValue,
                    PrimaryProcedureDescription = procedureCode.ShortDescription
                },
                cancellationToken);

            // --- Return result based on modality ---

            var messages = new List<ProcessMessage>();
            if (!processStep.MemberId.HasValue || !processStep.MemberEnrollmentId.HasValue)
            {
                messages.Add(RadiologyProcessMessages.MemberInfoNotProvided());
            }

            return modality switch
            {
                ProcedureModality.Mri =>
                    await CreateMriResponseAsync(
                        context.ProcessId,
                        procedureCode.CodeValue,
                        messages,
                        cancellationToken),
                ProcedureModality.Ct =>
                    await CreateCtResponseAsync(
                        context.ProcessId,
                        procedureCode.CodeValue,
                        messages,
                        cancellationToken),
                _ =>
                    ProcessStepHandlerResult<StartRadiologyIntakeResponse>.Success(
                        new StartRadiologyIntakeResponse(),
                        messages: messages.ToArray())
            };

            async Task<ProcessStepHandlerResult<StartRadiologyIntakeResponse>> CreateMriResponseAsync(
                Guid processId,
                string procedureCodeValue,
                List<ProcessMessage> messages,
                CancellationToken ct)
            {
                var questionnaire =
                    await questionnaireDefinitionClient.ResolveAsync(
                        processId,
                        nameof(CaptureMriInfoStep).Replace("Step", string.Empty),
                        ProcedureModality.Mri,
                        procedureCodeValue,
                        ct);

                return ProcessStepHandlerResult<StartRadiologyIntakeResponse>.Success(
                    new StartRadiologyIntakeResponse
                    {
                        QuestionnaireId = questionnaire?.QuestionnaireId,
                        QuestionnaireVersion = questionnaire?.Version,
                        Questionnaire = questionnaire
                    },
                    requiredStep: nameof(CaptureMriInfoStep).Replace("Step", string.Empty),
                    targetProcessorName: null,
                    messages.ToArray());
            }

            async Task<ProcessStepHandlerResult<StartRadiologyIntakeResponse>> CreateCtResponseAsync(
                Guid processId,
                string procedureCodeValue,
                List<ProcessMessage> messages,
                CancellationToken ct)
            {
                var questionnaire =
                    await questionnaireDefinitionClient.ResolveAsync(
                        processId,
                        nameof(CaptureMriInfoStep).Replace("Step", string.Empty),
                        ProcedureModality.Mri,
                        procedureCodeValue,
                        ct);

                return ProcessStepHandlerResult<StartRadiologyIntakeResponse>.Success(
                    new StartRadiologyIntakeResponse
                    {
                        QuestionnaireId = questionnaire?.QuestionnaireId,
                        QuestionnaireVersion = questionnaire?.Version,
                        Questionnaire = questionnaire
                    },
                    requiredStep: nameof(ConfirmCtInsteadOfMriStep).Replace("Step", string.Empty),
                    targetProcessorName: null,
                    messages.ToArray());
            }
        }
        catch (KaleidoQueryableClientException ex)
        {
            return ProcessStepHandlerResult<StartRadiologyIntakeResponse>.Failure(
                new StartRadiologyIntakeResponse(),
                RadiologyProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
    }
}
