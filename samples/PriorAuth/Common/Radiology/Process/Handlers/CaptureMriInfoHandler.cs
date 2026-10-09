using Kaleido.Http.Client;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

public sealed class CaptureMriInfoHandler(
    RadiologyDbContext dbContext,
    MriProcedureCodeResolverClient mriProcedureCodeResolverClient)
    : IProcessStepHandler<CaptureMriInfoStep>
{
    // Item ids of the configured MRI questionnaire (Configuration seed assets).
    private const string BodyPartItem = "body-part";
    private const string LateralityItem = "laterality";
    private const string ContrastItem = "contrast";

    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureMriInfoStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        // Kaleido has already checked the answers fit the presented questions; translating them
        // into the domain's own values is the handler's job.
        if (!TryRead(processStep, BodyPartItem, out MriBodyPart bodyPart, out var failure) ||
            !TryRead(processStep, LateralityItem, out Laterality laterality, out failure) ||
            !TryRead(processStep, ContrastItem, out ContrastOption contrast, out failure))
        {
            return ProcessStepHandlerResult.Failure(failure!);
        }

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
                    bodyPart,
                    laterality,
                    contrast,
                    cancellationToken);

            if (resolvedRule is null)
            {
                return ProcessStepHandlerResult.Success();
            }

            requestedService.ResolvedCodeValue = resolvedRule.ResolvedCodeValue;
            requestedService.ResolvedCodeSystem = resolvedRule.ResolvedCodeSystem;
            requestedService.Description = $"MRI {bodyPart}";

            await dbContext.SaveChangesAsync(cancellationToken);

            if (requestedService.ResolvedCodeValue == originalCodeValue && requestedService.ResolvedCodeSystem == originalCodeSystem)
            {
                return ProcessStepHandlerResult.Success();
            }

            return ProcessStepHandlerResult.Success(
                RadiologyProcessMessages.ProcedureCodeUpdated(
                    originalCodeSystem,
                    originalCodeValue,
                    requestedService.ResolvedCodeSystem,
                    requestedService.ResolvedCodeValue));
        }
        catch (KaleidoHttpClientException ex)
        {
            return ProcessStepHandlerResult.Failure(
                RadiologyProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
    }

    private static bool TryRead<TEnum>(
        CaptureMriInfoStep answers,
        string itemId,
        out TEnum value,
        out ProcessMessage? failure)
        where TEnum : struct, Enum
    {
        var answer =
            answers.Items
                .FirstOrDefault(x => x.ItemId == itemId)?
                .Answers
                .FirstOrDefault()?
                .Value;

        failure =
            Enum.TryParse(answer, ignoreCase: true, out value)
                ? null
                : RadiologyProcessMessages.UnexpectedAnswer(itemId, answer);

        return failure is null;
    }
}
