using Kaleido.Process.Attributes;
using Kaleido.Process.Shared.Entities;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Process.Shared.Steps;

[ProcessStep(
    "SubmitBilling",
    "Collects billing information.",
    "1.0")]
[DependsOnStep(typeof(StartOrderStep))]
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

public sealed record SubmitBillingResponse
{
    public required string BillingId { get; init; }

    public required PaymentMethodType PaymentMethod { get; init; }

    public required bool Accepted { get; init; }
    public decimal? AuthorizedAmount { get; internal set; }
    public string[] ValidationWarnings { get; internal set; }
}