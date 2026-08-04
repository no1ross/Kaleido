using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Process.Shared.Steps;

[ProcessStep(
    "ChangePaymentInfo",
    "Updates payment information.",
    "1.0")]
[DependsOnStep(typeof(SubmitOrderStep))]
public sealed record ChangePaymentInfoStep
{
    [Required]
    public required string OrderId { get; init; }

    [Required]
    public required PaymentMethodType PaymentMethod { get; init; }

    [Required]
    [StringLength(200)]
    public required string PaymentToken { get; init; }

    public string? Reason { get; init; }
}

public sealed record ChangePaymentInfoResponse
{
    public required bool Updated { get; init; }

    public required PaymentMethodType PaymentMethod { get; init; }

    public required string ConfirmationNumber { get; init; }

    public DateTimeOffset? ExpiresOn { get; init; }
}