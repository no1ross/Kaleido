using Kaleido.Samples.PriorAuth.Auth;

namespace Kaleido.Samples.PriorAuth.History.Process.Steps;

// Service-to-service only: Intake/Radiology record history on the user's behalf, from inside their
// own steps. Each call is the full current snapshot of the request as the calling processor knows
// it; History keeps the latest one per process. The recording processor is the calling processor
// Kaleido stamps on the call, never a payload field.
[KaleidoAuthorization(Policy = DevAuthPolicies.InternalCaller)]
[ProcessStep(
    DisplayName = "History - Upsert Record",
    Description = "Records the latest snapshot of a prior authorization request (one record per process), as reported by the calling processor.",
    Version = "2.0.0")]
[Repeatable]
public sealed record UpsertPriorAuthRecordStep : IProcessStep
{
    /// <summary>The product processor's prior authorization; null while the request is in Intake.</summary>
    public Guid? PriorAuthorizationId { get; init; }

    public PriorAuthorizationStatus Status { get; init; }

    public string MemberNumber { get; init; } = string.Empty;

    public string MemberDisplayName { get; init; } = string.Empty;

    public DateOnly? DateOfService { get; init; }

    public string PrimaryProcedureCode { get; init; } = string.Empty;

    public string PrimaryProcedureDescription { get; init; } = string.Empty;
}
