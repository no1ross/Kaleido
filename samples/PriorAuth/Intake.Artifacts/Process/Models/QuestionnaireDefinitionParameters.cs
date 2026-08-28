using Kaleido.Samples.PriorAuth.Configuration.Artifacts;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;

public sealed record QuestionnaireDefinitionParameters
{
    public string StepName { get; init; } = string.Empty;

    public string? PlanId { get; init; }

    public string? LineOfBusiness { get; init; }

    public ProcedureModality ProcedureModality { get; init; }

    public string? ProcedureCodeValue { get; init; }
}
