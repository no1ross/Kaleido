using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Process.Shared.Steps;

[ProcessStep(
    "CancelOrder",
    "Cancels a submitted order.",
    "1.0")]
[DependsOnStep(typeof(SubmitOrderStep))]
public sealed record CancelOrderStep
{
    [Required]
    public required string OrderId { get; init; }

    [Required]
    [StringLength(500)]
    public required string CancellationReason { get; init; }

    public bool RefundRequested { get; init; }
}

public sealed record CancelOrderResponse
{
    public required string CancellationNumber { get; init; }

    public required bool Cancelled { get; init; }

    public RefundInformation? Refund { get; init; }
}

public sealed record RefundInformation
{
    public required decimal Amount { get; init; }

    public required DateTimeOffset ProcessedOn { get; init; }

    public required RefundMethod RefundMethod { get; init; }
}
