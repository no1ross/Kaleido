using Kaleido.Samples.ECommerce.Process;

namespace Kaleido.Samples.ECommerce.Process.Responses;

public sealed record StartOrderResponse
{
    public required string OrderId { get; init; }

    public required DateTimeOffset CreatedOn { get; init; }

    public required OrderPriority Priority { get; init; }

    public string? Notes { get; init; }
}