using Kaleido.Process.Attributes;
using Kaleido.Process.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Process.Shared.Steps;

[ProcessStep(
    "StartOrder",
    "Creates an order from the shopping cart.",
    "1.0")]
[DependsOnStep(typeof(AddItemToCartStep))]
public sealed record StartOrderStep
{
    [Required]
    public required string CartId { get; init; }

    [Required]
    public required string MemberId { get; init; }

    [Required]
    public required OrderPriority Priority { get; init; }

    [Required]
    public required Address ShippingAddress { get; init; }
}

public sealed record StartOrderResponse
{
    public required string OrderId { get; init; }

    public required DateTimeOffset CreatedOn { get; init; }

    public required OrderPriority Priority { get; init; }

    public string? Notes { get; init; }
}