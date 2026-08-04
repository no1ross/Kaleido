using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Process.Shared.Steps;

[ProcessStep(
    "SubmitOrder",
    "Submits the completed order.",
    "1.0")]
[DependsOnStep(typeof(SubmitBillingStep))]
[DependsOnStep(typeof(AcceptTermsAndConditionsStep))]
public sealed record SubmitOrderStep
{
    [Required]
    public required string OrderId { get; init; }

    [StringLength(250)]
    public string? Comments { get; init; }
}

public sealed record SubmitOrderResponse
{
    public required string SubmissionId { get; init; }

    public required bool Submitted { get; init; }

    public bool RequiresPaymentCorrection { get; init; }

    public IReadOnlyCollection<ProcessIssue> Issues
    {
        get;
        init;
    } = [];
}

public sealed record ProcessIssue
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public Severity Severity { get; init; }
}