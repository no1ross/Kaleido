namespace Kaleido.Samples.PriorAuth.Clinical;

/// <summary>
/// Decides the clinical questions of a prior authorization, round by round: the caller answers, the
/// provider evaluates and returns the next questions, until it is done. Behind this seam may be the
/// business's configured pathways or a third-party criteria service; specialty processors (Radiology
/// first, others later) only ask the provider. Kaleido carries the questions and never decides them.
/// </summary>
public interface IClinicalQuestionProvider
{
    /// <summary>The first round for this request, or null when no clinical review applies.</summary>
    Task<ClinicalRound?> StartAsync(
        ClinicalContext context,
        CancellationToken cancellationToken);

    /// <summary>After the caller answered <paramref name="round"/>: the next round, or done.</summary>
    Task<ClinicalNext> ContinueAsync(
        ClinicalContext context,
        ClinicalRound round,
        IInformationStep answers,
        CancellationToken cancellationToken);
}

/// <summary>What a provider may use to choose and evaluate questions.</summary>
public sealed record ClinicalContext
{
    public required Guid ProcessId { get; init; }

    /// <summary>The information step the questions are presented with.</summary>
    public required string StepName { get; init; }

    public string? PlanId { get; init; }

    public string? LineOfBusiness { get; init; }

    public ProcedureModality ProcedureModality { get; init; }

    public string? ProcedureCodeValue { get; init; }

    /// <summary>What the caller already stated (binding key → value), proposed where questions match.</summary>
    public IReadOnlyDictionary<string, string> StatedDetails { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>One round of questions.</summary>
/// <param name="RoundId">The provider's id for this round (a questionnaire id, an external step key...).</param>
/// <param name="SessionId">The provider's own session or assessment id, kept with the request between rounds.</param>
public sealed record ClinicalRound(
    string RoundId,
    InformationRequest Request,
    string? SessionId = null);

/// <summary>The next round, or the end of the clinical review with the provider's determination (recorded, not decided here).</summary>
public sealed record ClinicalNext(
    ClinicalRound? Next,
    string? Determination = null)
{
    public static ClinicalNext Done(string? determination) => new(null, determination);
}
