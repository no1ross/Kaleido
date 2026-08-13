namespace Kaleido.Samples.ECommerce.Process.Responses;

public sealed record CancelOrderResponse
{
    public required string CancellationNumber { get; init; }

    public required bool Cancelled { get; init; }

    public RefundInformation? Refund { get; init; }
}
