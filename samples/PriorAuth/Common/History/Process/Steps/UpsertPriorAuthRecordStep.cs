using System.ComponentModel;
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
    [Description("The prior authorization assigned by the product processor; empty while the request is in Intake.")]
    public Guid? PriorAuthorizationId { get; init; }

    [Description("The current status of the prior authorization request.")]
    public PriorAuthorizationStatus Status { get; init; }

    [Description("The member number of the member the request is for.")]
    public string MemberNumber { get; init; } = string.Empty;

    [Description("The display name of the member the request is for.")]
    public string MemberDisplayName { get; init; } = string.Empty;

    [Description("The date the service is planned, when known.")]
    public DateOnly? DateOfService { get; init; }

    [Description("The procedure code of the main service requested.")]
    public string PrimaryProcedureCode { get; init; } = string.Empty;

    [Description("A readable description of the main service requested.")]
    public string PrimaryProcedureDescription { get; init; } = string.Empty;
}
