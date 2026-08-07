namespace Kaleido.Process.Shared.Responses;

public sealed record RefundInformation
{
    public required decimal Amount { get; init; }

    public required DateTimeOffset ProcessedOn { get; init; }

    public required RefundMethod RefundMethod { get; init; }
}
