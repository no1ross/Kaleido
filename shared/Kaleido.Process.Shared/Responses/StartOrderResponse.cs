using Kaleido.Samples.ECommerce;

namespace Kaleido.Samples.ECommerce.Responses;

public sealed record StartOrderResponse
{
    public required string OrderId { get; init; }

    public required DateTimeOffset CreatedOn { get; init; }

    public required OrderPriority Priority { get; init; }

    public string? Notes { get; init; }
}