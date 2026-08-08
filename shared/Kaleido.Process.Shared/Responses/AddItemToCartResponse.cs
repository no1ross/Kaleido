namespace Kaleido.Samples.ECommerce.Responses;

public sealed record AddItemToCartResponse
{
    public required string CartId { get; init; }

    public required int ItemCount { get; init; }

    public required decimal CartTotal { get; init; }

    public required DateTimeOffset LastUpdated { get; init; }
}