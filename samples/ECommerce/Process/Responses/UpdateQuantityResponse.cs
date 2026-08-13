namespace Kaleido.Samples.ECommerce.Process.Responses;

public sealed record UpdateQuantityResponse
{
    public required string CartId { get; init; }

    public required string ItemId { get; init; }

    public required int Quantity { get; init; }

    public required decimal CartTotal { get; init; }
}