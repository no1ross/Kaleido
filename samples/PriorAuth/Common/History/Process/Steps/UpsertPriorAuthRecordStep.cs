using System.ComponentModel.DataAnnotations;
using Kaleido.Samples.PriorAuth.Auth;

namespace Kaleido.Samples.PriorAuth.History.Process.Steps;

// Service-to-service only: Intake/Radiology record history on the user's behalf.
[KaleidoAuthorization(Policy = DevAuthPolicies.InternalCaller)]
[ProcessStep(
    Name = "UpsertPriorAuthRecord",
    DisplayName = "History - Upsert Record",
    Description = "Creates or updates the prior authorization history record for the given process.",
    Version = "1.0.0")]
public sealed record UpsertPriorAuthRecordStep
{
    [Required]
    [StringLength(100)]
    public string ProcessorName { get; init; } = string.Empty;

    public PriorAuthorizationStatus Status { get; init; }

    public string MemberNumber { get; init; } = string.Empty;

    public string MemberDisplayName { get; init; } = string.Empty;

    public DateOnly DateOfService { get; init; }

    public string PrimaryProcedureCode { get; init; } = string.Empty;

    public string PrimaryProcedureDescription { get; init; } = string.Empty;
}
