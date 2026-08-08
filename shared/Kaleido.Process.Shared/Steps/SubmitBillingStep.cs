using Kaleido.Process.Attributes;
using Kaleido.Samples.ECommerce;
using Kaleido.Samples.ECommerce.Entities;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Steps;

[ProcessStep(
    Name = "SubmitBilling",
    DisplayName = "Provide Billing Information",
    Description = "Captures and validates billing information required to complete an order.",
    Version = "1.0")]
[AvailableAfter(typeof(StartOrderStep))]
[AvailableUntil(typeof(SubmitOrderStep))]
[Repeatable]
public sealed record SubmitBillingStep
{
    [Required]
    public required string OrderId { get; init; }

    [Required]
    public required PaymentMethodType PaymentMethod { get; init; }

    [Required]
    [StringLength(200)]
    public required string PaymentToken { get; init; }

    [Required]
    public required Address BillingAddress { get; init; }
}
