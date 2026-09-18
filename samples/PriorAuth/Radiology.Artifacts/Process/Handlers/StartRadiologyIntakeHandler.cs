using Kaleido.Process;
using Kaleido.Process.Execution;
using Kaleido.Queryable.Http.Client;
using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Kaleido.Samples.PriorAuth.Radiology;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Models;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.History.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

/// <summary>
/// Handles the <see cref="StartRadiologyIntakeStep"/> submitted by the Intake processor
/// during a cross-processor handoff. Creates the prior authorization and its first requested
/// service. If member information is provided it validates eligibility and routes directly
/// to the appropriate modality step; otherwise routes to <see cref="ValidateMemberStep"/>.
/// </summary>
public sealed class StartRadiologyIntakeHandler(
    RadiologyDbContext dbContext,
    IMemberEligibilityService memberEligibilityService,
    ProcedureCodeClient procedureCodeClient,
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

            // --- Upsert PriorAuthorization ---

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

            // --- Route based on member presence ---

            if (!processStep.MemberId.HasValue || !processStep.MemberEnrollmentId.HasValue)
            {
                // No member provided — require ValidateMember next
                return ProcessStepHandlerResult<StartRadiologyIntakeResponse>.Success(
                    new StartRadiologyIntakeResponse(),
                    requiredStep: nameof(ValidateMemberStep).Replace("Step", string.Empty),
                    messages: RadiologyProcessMessages.MemberInfoNotProvided());
            }

            // Member provided — validate eligibility and route by modality
            var eligibility =
                await memberEligibilityService.ValidateAsync(
                    processStep.MemberId.Value,
                    processStep.MemberEnrollmentId.Value,
                    processStep.DateOfService ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    context.ProcessId,
                    cancellationToken);

            if (!eligibility.Succeeded)
            {
                return ProcessStepHandlerResult<StartRadiologyIntakeResponse>.Failure(
                    new StartRadiologyIntakeResponse(),
                    eligibility.FailureMessage!);
            }

            // Persist member onto the PriorAuthorization
            if (priorAuthorization.Member is null)
            {
                priorAuthorization.Member =
                    new PriorAuthorizationMember
                    {
                        PriorAuthorizationId = priorAuthorization.PriorAuthorizationId
                    };
            }

            var memberDetails = eligibility.MemberDetails!;
            priorAuthorization.Member.MemberId = memberDetails.MemberId;
            priorAuthorization.Member.MemberEnrollmentId = memberDetails.MemberEnrollmentId;
            priorAuthorization.Member.MemberNumber = memberDetails.MemberNumber;
            priorAuthorization.Member.DisplayName = memberDetails.DisplayName;
            priorAuthorization.Member.PlanId = memberDetails.PlanId;
            priorAuthorization.Member.PlanName = memberDetails.PlanName;
            priorAuthorization.Member.LineOfBusiness = memberDetails.LineOfBusiness;

            await dbContext.SaveChangesAsync(cancellationToken);

            var routing =
                await memberEligibilityService.RouteByModalityAsync(
                    context.ProcessId,
                    memberDetails,
                    cancellationToken);

            if (!routing.Succeeded)
            {
                return ProcessStepHandlerResult<StartRadiologyIntakeResponse>.Failure(
                    new StartRadiologyIntakeResponse(),
                    routing.FailureMessage!);
            }

            return ProcessStepHandlerResult<StartRadiologyIntakeResponse>.Success(
                new StartRadiologyIntakeResponse
                {
                    QuestionnaireId = routing.Questionnaire?.QuestionnaireId,
                    QuestionnaireVersion = routing.Questionnaire?.Version,
                    Questionnaire = routing.Questionnaire
                },
                requiredStep: routing.RequiredStep);
        }
        catch (KaleidoQueryableClientException ex)
        {
            Console.WriteLine($"[Radiology StartRadiologyIntake] KaleidoQueryableClientException caught for process {context.ProcessId}: {ex.Message}");
            return ProcessStepHandlerResult<StartRadiologyIntakeResponse>.Failure(
                new StartRadiologyIntakeResponse(),
                RadiologyProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
    }
}
