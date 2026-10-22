using Kaleido.Http.Client;
using Kaleido.Samples.PriorAuth.History.Process;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Models;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

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
    public Task<ProcessStepHandlerResult<StartRadiologyIntakeResponse>> ExecuteAsync(
        StartRadiologyIntakeStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken) =>
        HistoryRecording.InTransactionAsync<ProcessStepHandlerResult<StartRadiologyIntakeResponse>>(
            dbContext,
            () => RunAsync(processStep, context, cancellationToken),
            _ => ProcessStepHandlerResult.Reject(RadiologyProcessMessages.HistoryNotRecorded(context.ProcessId)),
            cancellationToken);

    private async Task<ProcessStepHandlerResult<StartRadiologyIntakeResponse>> RunAsync(
        StartRadiologyIntakeStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
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
                return ProcessStepHandlerResult.Reject(RadiologyProcessMessages.ProcedureCodeNotFound(
                        processStep.CodeSystem,
                        processStep.CodeValue));
            }

            // --- Eligibility, before anything is written, so a rejected start leaves nothing behind ---

            MemberEligibilityResult? eligibility = null;

            if (processStep.MemberId.HasValue && processStep.MemberEnrollmentId.HasValue)
            {
                eligibility =
                    await memberEligibilityService.CheckEligibilityAsync(
                        processStep.MemberId.Value,
                        processStep.MemberEnrollmentId.Value,
                        processStep.DateOfService ?? DateOnly.FromDateTime(DateTime.UtcNow),
                        cancellationToken);

                if (!eligibility.Succeeded)
                {
                    return ProcessStepHandlerResult.Reject(
                        eligibility.FailureMessage!);
                }
            }

            // --- Upsert PriorAuthorization ---

            var priorAuthorization =
                await dbContext.PriorAuthorizations
                    .Include(x => x.Member)
                    .Include(x => x.RequestedServices)
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

            priorAuthorization.DateOfService = processStep.DateOfService ?? priorAuthorization.DateOfService;

            // --- Add requested service (once: a retried start does not add it again) ---

            if (!priorAuthorization.RequestedServices.Any(x =>
                    x.ResolvedCodeValue == procedureCode.CodeValue &&
                    x.ResolvedCodeSystem == procedureCode.CodeSystem))
            {
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
                        Description = procedureCode.ShortDescription,
                        CreatedUtc = DateTimeOffset.UtcNow,
                        StatedDetailsJson = StatedServiceDetails.ToJson(processStep.BodyPart, processStep.Laterality, processStep.Contrast)
                    });
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // --- Route based on member presence ---

            if (eligibility is null)
            {
                await historyClient.RecordAsync(context.ProcessId, cancellationToken);

                // No member provided — require ValidateMember next
                return ProcessStepHandlerResult.Success(
                    new StartRadiologyIntakeResponse(),
                    RadiologyProcessMessages.MemberInfoNotProvided()).Next<ValidateMemberStep>();
            }

            // Member provided and eligible (checked before anything was written): record it and route by modality

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

            // The date eligibility was checked for.
            priorAuthorization.DateOfService ??= DateOnly.FromDateTime(DateTime.UtcNow);

            await dbContext.SaveChangesAsync(cancellationToken);

            await historyClient.RecordAsync(context.ProcessId, cancellationToken);

            var routing =
                await memberEligibilityService.RouteByModalityAsync(
                    context.ProcessId,
                    memberDetails,
                    cancellationToken);

            if (!routing.Succeeded)
            {
                return ProcessStepHandlerResult.Reject(routing.FailureMessage!);
            }

            var response =
                new StartRadiologyIntakeResponse();

            return routing.Route == ModalityRoute.CaptureMriInfo
                ? ProcessStepHandlerResult.Success(response).RequireInformation<CaptureMriInfoStep>(routing.InformationRequest!)
                : ProcessStepHandlerResult.Success(response).RequireInformation<ConfirmCtInsteadOfMriStep>(routing.InformationRequest!);
        }
        catch (KaleidoHttpClientException ex)
        {
            return ProcessStepHandlerResult.Reject(RadiologyProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
    }
}
