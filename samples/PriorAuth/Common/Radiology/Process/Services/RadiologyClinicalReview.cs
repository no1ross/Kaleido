using Kaleido.Samples.PriorAuth.Clinical;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

/// <summary>
/// Runs Radiology's clinical review through the configured <see cref="IClinicalQuestionProvider"/>:
/// starts it, and after each round of answers asks the provider for the next round or its
/// determination. Each round is kept (questions and answers) for review; the provider's session id,
/// current round and determination are kept with the request. Radiology never decides the next
/// question.
/// </summary>
public sealed class RadiologyClinicalReview(
    IClinicalQuestionProvider provider,
    RadiologyDbContext dbContext,
    ProcedureModalityClient procedureModalityClient,
    ConfiguredInformationRequests informationRequests)
{
    private const string StepName = nameof(CaptureClinicalInfoStep);

    /// <summary>The first round's questions, or null when no clinical review applies.</summary>
    public async Task<InformationRequest?> StartAsync(
        Guid processId,
        CancellationToken cancellationToken)
    {
        var context = await ContextAsync(processId, cancellationToken);
        var round = await provider.StartAsync(context, cancellationToken);

        return round is null ? null : await PresentAsync(processId, round, cancellationToken);
    }

    /// <summary>Records the answers to the current round; returns the next round's questions, or null when the review is done.</summary>
    public async Task<InformationRequest?> ContinueAsync(
        Guid processId,
        IInformationStep answers,
        CancellationToken cancellationToken)
    {
        var priorAuthorization = await dbContext.PriorAuthorizations.SingleAsync(x => x.ProcessId == processId, cancellationToken);

        await informationRequests.RecordAnswersAsync(processId, Key(priorAuthorization.ClinicalRoundCount), answers, cancellationToken);

        var current = new ClinicalRound(
            priorAuthorization.ClinicalRoundId ?? string.Empty,
            new InformationRequest { InformationRequestId = answers.InformationRequestId },
            priorAuthorization.ClinicalSessionId);

        var next = await provider.ContinueAsync(await ContextAsync(processId, cancellationToken), current, answers, cancellationToken);

        if (next.Next is { } round)
        {
            return await PresentAsync(processId, round, cancellationToken);
        }

        priorAuthorization.ClinicalRoundId = null;
        priorAuthorization.ClinicalDetermination = next.Determination;

        await dbContext.SaveChangesAsync(cancellationToken);

        return null;
    }

    private async Task<InformationRequest> PresentAsync(
        Guid processId,
        ClinicalRound round,
        CancellationToken cancellationToken)
    {
        var priorAuthorization = await dbContext.PriorAuthorizations.SingleAsync(x => x.ProcessId == processId, cancellationToken);

        priorAuthorization.ClinicalRoundCount++;
        priorAuthorization.ClinicalRoundId = round.RoundId;
        priorAuthorization.ClinicalSessionId = round.SessionId ?? priorAuthorization.ClinicalSessionId;

        await dbContext.SaveChangesAsync(cancellationToken);

        await informationRequests.RecordRequestAsync(
            processId,
            Key(priorAuthorization.ClinicalRoundCount),
            round.RoundId,
            string.Empty,
            round.Request,
            cancellationToken);

        return round.Request;
    }

    private async Task<ClinicalContext> ContextAsync(
        Guid processId,
        CancellationToken cancellationToken)
    {
        var priorAuthorization =
            await dbContext.PriorAuthorizations
                .AsNoTracking()
                .Include(x => x.Member)
                .Include(x => x.RequestedServices)
                .SingleAsync(x => x.ProcessId == processId, cancellationToken);

        // The clinical questions are about the primary (first) requested service.
        var primary = priorAuthorization.RequestedServices.MinBy(x => x.CreatedUtc);

        return new ClinicalContext
        {
            ProcessId = processId,
            StepName = StepName,
            PlanId = priorAuthorization.Member?.PlanId,
            LineOfBusiness = priorAuthorization.Member?.LineOfBusiness.ToString(),
            ProcedureModality = primary is null
                ? ProcedureModality.Unknown
                : await procedureModalityClient.DetermineModalityAsync(primary.ResolvedCodeValue, primary.ResolvedCodeSystem, cancellationToken),
            ProcedureCodeValue = primary?.ResolvedCodeValue,
            StatedDetails = await informationRequests.StatedDetailsAsync(processId, cancellationToken)
        };
    }

    private static string Key(int round) => $"{StepName}#{round}";
}
