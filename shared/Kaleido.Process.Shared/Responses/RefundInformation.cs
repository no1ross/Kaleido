using Kaleido.Samples.ECommerce;

namespace Kaleido.Samples.ECommerce.Responses;

public sealed record RefundInformation
{
    public required decimal Amount { get; init; }

    public required DateTimeOffset ProcessedOn { get; init; }

    public required RefundMethod RefundMethod { get; init; }
}
