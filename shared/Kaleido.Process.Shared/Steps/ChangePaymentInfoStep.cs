using Kaleido.Process.Attributes;
using Kaleido.Process.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Process.Shared.Steps;

[ProcessStep(
    Name = "ChangePaymentInfo",
    DisplayName = "Update Payment Information",
    Description = "Updates payment information for an existing order.",
    Version = "1.0")]
[AvailableAfter(typeof(SubmitOrderStep))]
[Repeatable]
public sealed record ChangePaymentInfoStep
{
    [Required]
    public required string OrderId { get; init; }

    [Required]
    public required PaymentMethodType PaymentMethod { get; init; }

    [Required]
    [StringLength(200)]
    public required string PaymentToken { get; init; }

    public Address BillingAddress { get; init; }

    public string? Reason { get; init; }
}
