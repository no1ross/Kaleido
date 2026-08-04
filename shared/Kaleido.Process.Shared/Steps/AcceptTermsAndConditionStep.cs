using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Process.Shared.Steps;

[ProcessStep(
    "AcceptTermsAndConditions",
    "Accepts required terms and conditions.",
    "1.0")]
[DependsOnStep(typeof(StartOrderStep))]
public sealed record AcceptTermsAndConditionsStep
{
    [Required]
    public required string OrderId { get; init; }

    [Required]
    public required bool Accepted { get; init; }

    [Required]
    public required DateTimeOffset AcceptedOn { get; init; }
}

public sealed record AcceptTermsAndConditionsResponse
{
    public required bool Accepted { get; init; }

    public required string TermsVersion { get; init; }

    public required DateTimeOffset AcceptedOn { get; init; }
}