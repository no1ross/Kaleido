namespace Kaleido.Samples.ECommerce.Responses;

public sealed record RemoveItemResponse
{
    public required string CartId { get; init; }

    public required string ItemId { get; init; }

    public required bool Removed { get; init; }

    public required int RemainingItems { get; init; }
}


