namespace Kaleido.Samples.PriorAuth.Intake.Data.Entities;

public sealed class PriorAuthorizationQuestionnaireAssignment
{
    public Guid PriorAuthorizationQuestionnaireAssignmentId { get; set; }

    public Guid ProcessId { get; set; }

    public string StepName { get; set; } = string.Empty;

    public string QuestionnaireId { get; set; } = string.Empty;

    public string QuestionnaireVersion { get; set; } = string.Empty;

    public DateTimeOffset AssignedUtc { get; set; }
}
