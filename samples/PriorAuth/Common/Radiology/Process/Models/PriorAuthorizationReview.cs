using System.ComponentModel;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Models;

/// <summary>The whole request as captured, for the caller to review before submitting.</summary>
public sealed record PriorAuthorizationReview
{
    [Description("The request's status (draft until submitted).")]
    public PriorAuthorizationStatus Status { get; init; }

    [Description("The member: name, member ID and plan.")]
    public ReviewedMember? Member { get; init; }

    [Description("The requested services: code, system and description.")]
    public IReadOnlyList<ReviewedService> RequestedServices { get; init; } = [];

    [Description("The requesting (ordering) provider.")]
    public ReviewedProvider? RequestingProvider { get; init; }

    [Description("The servicing provider (facility), with network status.")]
    public ReviewedProvider? ServicingProvider { get; init; }

    [Description("The clinical question provider's determination when the clinical review finished (recorded, not decided by Radiology), e.g. CriteriaMet or ClinicalReviewRequired.")]
    public string? ClinicalDetermination { get; init; }

    [Description("Every answered questionnaire, clinical rounds included: the questions as asked and the caller's answers.")]
    public IReadOnlyList<RecordedQuestionnaire> Answers { get; init; } = [];
}

public sealed record ReviewedMember(string DisplayName, string MemberNumber, string PlanName);

public sealed record ReviewedService(string CodeValue, ProcedureCodeSystem CodeSystem, string Description);

public sealed record ReviewedProvider(string ProviderName, string? LocationName, bool? IsInNetwork);
