using Kaleido.Http.Client;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

public sealed class CaptureMriInfoHandler(
    RadiologyDbContext dbContext,
    MriProcedureCodeResolverClient mriProcedureCodeResolverClient,
    ConfiguredInformationRequests informationRequests)
    : IProcessStepHandler<CaptureMriInfoStep>
{
    // The binding keys the business configures on the MRI questionnaire; answers are read by them.
    private const string BodyPartItem = "BodyPart";
    private const string LateralityItem = "Laterality";
    private const string ContrastItem = "Contrast";

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
            await informationRequests.RecordAnswersAsync(context.ProcessId, nameof(CaptureMriInfoStep), processStep, cancellationToken);

            // The questions were about the service captured last. Ordered in memory: SQLite cannot
            // order by DateTimeOffset, and a request has only a few services.
            var requestedService =
                (await dbContext.PriorAuthorizationRequestedServices
                    .Where(x => x.PriorAuthorization.ProcessId == context.ProcessId)
                    .ToListAsync(cancellationToken))
                .MaxBy(x => x.CreatedUtc)
                ?? throw new InvalidOperationException($"Process {context.ProcessId} has no requested service.");

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
        var answer = ConfiguredInformationRequests.Answer(answers, itemId);

        failure =
            Enum.TryParse(answer, ignoreCase: true, out value)
                ? null
                : RadiologyProcessMessages.UnexpectedAnswer(itemId, answer);

        return failure is null;
    }
}
