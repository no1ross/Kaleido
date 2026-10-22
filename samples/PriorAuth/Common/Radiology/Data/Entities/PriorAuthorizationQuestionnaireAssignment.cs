using Kaleido.Samples.PriorAuth.Audit;

namespace Kaleido.Samples.PriorAuth.Radiology.Data.Entities;

/// <summary>
/// The configured questionnaire a step of this process presented, and the caller's answers once
/// given (one row per process and step).
/// </summary>
public sealed class PriorAuthorizationQuestionnaireAssignment : IAuditable
{
    public Guid PriorAuthorizationQuestionnaireAssignmentId { get; set; }

    public Guid ProcessId { get; set; }

    public string StepName { get; set; } = string.Empty;

    public string QuestionnaireId { get; set; } = string.Empty;

    public string QuestionnaireVersion { get; set; } = string.Empty;

    public string? Title { get; set; }

    public DateTimeOffset AssignedUtc { get; set; }

    /// <summary>The information request as presented (questions, options, proposals), as JSON.</summary>
    public string? RequestJson { get; set; }

    /// <summary>The questions and the caller's answers (question text and option display included), as JSON.</summary>
    public string? AnswersJson { get; set; }

    public DateTimeOffset? AnsweredUtc { get; set; }

    /// <inheritdoc />
    public string CreatedBy { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? CreatedVia { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedUtc { get; set; }

    /// <inheritdoc />
    public string UpdatedBy { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? UpdatedVia { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedUtc { get; set; }
}
