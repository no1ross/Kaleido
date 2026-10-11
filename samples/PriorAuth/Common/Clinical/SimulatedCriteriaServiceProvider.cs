namespace Kaleido.Samples.PriorAuth.Clinical;

/// <summary>
/// Stands in for a third-party clinical criteria service (a SaaS assessment API): it keeps its own
/// assessment id, speaks its own question format, and decides the next question with its own logic.
/// This provider only translates between that service and Kaleido's information requests, which is
/// what an integration with a real vendor would do. The decision tree below is the "vendor's" logic,
/// not Radiology's.
/// </summary>
public sealed class SimulatedCriteriaServiceProvider : IClinicalQuestionProvider
{
    // The vendor's question format.
    private sealed record VendorQuestion(string Key, string Prompt, string Kind, (string Value, string Label)[] Choices);

    private static readonly Dictionary<string, VendorQuestion> Questions = new(StringComparer.Ordinal)
    {
        ["reason"] = new("reason", "What is the main reason for the imaging study?", "choice",
            [("trauma", "Recent injury or trauma"), ("chronic", "Pain lasting longer than six weeks"), ("other", "Other")]),
        ["trauma-weight"] = new("trauma-weight", "Can the patient bear weight on the affected limb?", "boolean", []),
        ["chronic-therapy"] = new("chronic-therapy", "How many weeks of physical therapy or other conservative care has the patient completed?", "integer", []),
        ["xray"] = new("xray", "Were plain X-rays taken in the last 60 days?", "boolean", [])
    };

    public Task<ClinicalRound?> StartAsync(
        ClinicalContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ClinicalRound?>(Round("reason", sessionId: $"vendor-{Guid.NewGuid():N}"));

    public Task<ClinicalNext> ContinueAsync(
        ClinicalContext context,
        ClinicalRound round,
        IInformationStep answers,
        CancellationToken cancellationToken = default)
    {
        var answer = answers.Items.FirstOrDefault()?.Answers.FirstOrDefault()?.Value;

        // The vendor's decision tree.
        (string? Next, string? Determination) next = (round.RoundId, answer) switch
        {
            ("reason", "trauma") => ("trauma-weight", null),
            ("reason", "chronic") => ("chronic-therapy", null),
            ("reason", _) => ("xray", null),
            ("trauma-weight", "false") => (null, "CriteriaMet"),
            ("trauma-weight", _) => ("xray", null),
            ("chronic-therapy", var weeks) when int.TryParse(weeks, out var w) && w < 6 => (null, "CriteriaNotMet"),
            ("chronic-therapy", _) => ("xray", null),
            ("xray", "true") => (null, "CriteriaMet"),
            _ => (null, "ClinicalReviewRequired")
        };

        return Task.FromResult(next.Next is { } key
            ? new ClinicalNext(Round(key, round.SessionId))
            : ClinicalNext.Done(next.Determination));
    }

    private static ClinicalRound Round(string key, string? sessionId)
    {
        var question = Questions[key];

        return new ClinicalRound(
            key,
            new InformationRequest
            {
                InformationRequestId = $"vendor-criteria|{key}",
                Title = "Clinical criteria",
                Items =
                [
                    new InformationItem
                    {
                        Id = question.Key,
                        Text = question.Prompt,
                        Type = question.Kind switch
                        {
                            "choice" => InformationItemType.Choice,
                            "boolean" => InformationItemType.Boolean,
                            "integer" => InformationItemType.WholeNumber,
                            _ => InformationItemType.Text
                        },
                        Options = question.Choices.Select(x => new InformationOption { Value = x.Value, Display = x.Label }).ToArray()
                    }
                ]
            },
            sessionId);
    }
}
