using Kaleido.Samples.PriorAuth.Configuration;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Models;

public sealed record QuestionnaireDefinitionParameters
{
    public string StepName { get; init; } = string.Empty;

    public string? PlanId { get; init; }

    public string? LineOfBusiness { get; init; }

    public ProcedureModality ProcedureModality { get; init; }

    public string? ProcedureCodeValue { get; init; }
}
