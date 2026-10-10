namespace Kaleido.Samples.PriorAuth.Configuration.Data.Entities;

/// <summary>
/// One edge of a clinical pathway: after the caller answers <see cref="FromQuestionnaireId"/>, ask
/// <see cref="ToQuestionnaireId"/> when the answer to <see cref="WhenBindingKey"/> matches
/// <see cref="AnswerValue"/> (or always, when no condition is set). No matching transition means the
/// pathway is done. The business configures pathways; no code decides the next question.
/// </summary>
public sealed class ClinicalPathwayTransition
{
    public Guid ClinicalPathwayTransitionId { get; set; }

    public string FromQuestionnaireId { get; set; } = string.Empty;

    public string? WhenBindingKey { get; set; }

    /// <summary><c>Equals</c> (any answer equals the value) or <c>NotEquals</c> (no answer equals it).</summary>
    public string Operator { get; set; } = "Equals";

    public string? AnswerValue { get; set; }

    /// <summary>The next questionnaire, or null to finish the pathway with <see cref="Determination"/>.</summary>
    public string? ToQuestionnaireId { get; set; }

    /// <summary>The outcome recorded when the pathway finishes on this transition (e.g. <c>CriteriaMet</c>).</summary>
    public string? Determination { get; set; }

    /// <summary>Higher priority is evaluated first; conditional transitions should outrank the default one.</summary>
    public int Priority { get; set; }
}