using Kaleido.Processor;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Views;
using Kaleido.Samples.PriorAuth.Member.Queryable.ViewSources.Views;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

public interface IMemberEligibilityService
{
    Task<MemberEligibilityResult> ValidateAsync(
        Guid memberId,
        Guid memberEnrollmentId,
        DateOnly dateOfService,
        Guid processId,
        CancellationToken cancellationToken = default);

    Task<ModalityRoutingResult> RouteByModalityAsync(
        Guid processId,
        MemberDetailsView memberDetails,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Shared member eligibility validation and modality routing logic used by
/// <see cref="Handlers.ValidateMemberHandler"/>, <see cref="Handlers.CaptureMemberHandler"/>,
/// and <see cref="Handlers.StartRadiologyIntakeHandler"/>.
/// </summary>
public sealed class MemberEligibilityService(
    RadiologyDbContext dbContext,
    MemberDetailsClient memberDetailsClient,
    ProcedureModalityClient procedureModalityClient,
    QuestionnaireDefinitionClient questionnaireDefinitionClient)
    : IMemberEligibilityService
{
    /// <summary>
    /// Validates that a prior authorization exists for the process and that the
    /// member is eligible (exists in the database, active enrollment).
    /// </summary>
    /// <returns>
    /// A <see cref="MemberEligibilityResult"/> indicating success with the member
    /// details, or failure with a <see cref="ProcessMessage"/> describing the problem.
    /// </returns>
    public async Task<MemberEligibilityResult> ValidateAsync(
        Guid memberId,
        Guid memberEnrollmentId,
        DateOnly dateOfService,
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        var priorAuthExists =
            await dbContext.PriorAuthorizations
                .AnyAsync(
                    x => x.ProcessId == processId,
                    cancellationToken);

        if (!priorAuthExists)
        {
            return MemberEligibilityResult.Fail(
                RadiologyProcessMessages.PriorAuthorizationNotFound(processId));
        }

        var memberDetails =
            await memberDetailsClient.GetMemberDetailsAsync(
                memberId,
                memberEnrollmentId,
                cancellationToken);

        if (memberDetails is null)
        {
            return MemberEligibilityResult.Fail(
                RadiologyProcessMessages.MemberNotFound(memberId, memberEnrollmentId));
        }

        if (dateOfService < memberDetails.EffectiveDate)
        {
            return MemberEligibilityResult.Fail(
                RadiologyProcessMessages.CoverageNotYetEffective(
                    memberEnrollmentId,
                    dateOfService,
                    memberDetails.EffectiveDate));
        }

        if (memberDetails.TerminationDate is DateOnly terminationDate
            && dateOfService > terminationDate)
        {
            return MemberEligibilityResult.Fail(
                RadiologyProcessMessages.CoverageTerminated(
                    memberEnrollmentId,
                    dateOfService,
                    terminationDate));
        }

        return MemberEligibilityResult.Ok(memberDetails);
    }

    /// <summary>
    /// Determines the modality of the first requested service on the prior
    /// authorization for <paramref name="processId"/> and returns the
    /// appropriate next step name and questionnaire.
    /// </summary>
    public async Task<ModalityRoutingResult> RouteByModalityAsync(
        Guid processId,
        MemberDetailsView memberDetails,
        CancellationToken cancellationToken = default)
    {
        var requestedService =
            await dbContext.PriorAuthorizationRequestedServices
                .AsNoTracking()
                .Where(x => x.PriorAuthorization!.ProcessId == processId)
                .Select(x => new { x.ResolvedCodeValue, x.ResolvedCodeSystem })
                .FirstOrDefaultAsync(cancellationToken);

        if (requestedService is null)
        {
            return ModalityRoutingResult.Fail(
                RadiologyProcessMessages.PriorAuthorizationNotFound(processId));
        }

        var modality =
            await procedureModalityClient.DetermineModalityAsync(
                requestedService.ResolvedCodeValue,
                requestedService.ResolvedCodeSystem,
                cancellationToken);

        return modality switch
        {
            ProcedureModality.Mri =>
                await BuildModalityResultAsync(
                    processId,
                    ModalityRoute.CaptureMriInfo,
                    nameof(CaptureMriInfoStep),
                    ProcedureModality.Mri,
                    requestedService.ResolvedCodeValue,
                    cancellationToken),

            ProcedureModality.Ct =>
                await BuildModalityResultAsync(
                    processId,
                    ModalityRoute.ConfirmCtInsteadOfMri,
                    nameof(ConfirmCtInsteadOfMriStep),
                    ProcedureModality.Mri,
                    requestedService.ResolvedCodeValue,
                    cancellationToken),

            _ =>
                ModalityRoutingResult.Fail(
                    RadiologyProcessMessages.ModalityNotSupported(
                        requestedService.ResolvedCodeSystem,
                        requestedService.ResolvedCodeValue,
                        modality))
        };
    }

    private async Task<ModalityRoutingResult> BuildModalityResultAsync(
        Guid processId,
        ModalityRoute route,
        string stepName,
        ProcedureModality modality,
        string procedureCodeValue,
        CancellationToken cancellationToken)
    {
        var questionnaire =
            await questionnaireDefinitionClient.ResolveAsync(
                processId,
                stepName,
                modality,
                procedureCodeValue,
                cancellationToken);

        return ModalityRoutingResult.Ok(route, questionnaire);
    }
}

public sealed record MemberEligibilityResult
{
    public bool Succeeded { get; init; }
    public MemberDetailsView? MemberDetails { get; init; }
    public ProcessMessage? FailureMessage { get; init; }

    public static MemberEligibilityResult Ok(MemberDetailsView memberDetails) =>
        new() { Succeeded = true, MemberDetails = memberDetails };

    public static MemberEligibilityResult Fail(ProcessMessage message) =>
        new() { Succeeded = false, FailureMessage = message };
}

public enum ModalityRoute
{
    CaptureMriInfo,
    ConfirmCtInsteadOfMri
}

public sealed record ModalityRoutingResult
{
    public bool Succeeded { get; init; }
    public ModalityRoute? Route { get; init; }
    public QuestionnaireDefinitionView? Questionnaire { get; init; }
    public ProcessMessage? FailureMessage { get; init; }

    public static ModalityRoutingResult Ok(
        ModalityRoute route,
        QuestionnaireDefinitionView? questionnaire) =>
        new() { Succeeded = true, Route = route, Questionnaire = questionnaire };

    public static ModalityRoutingResult Fail(ProcessMessage message) =>
        new() { Succeeded = false, FailureMessage = message };
}
