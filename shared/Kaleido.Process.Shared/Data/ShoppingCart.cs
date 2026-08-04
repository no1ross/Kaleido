namespace Kaleido.Process.Shared.Data;

public sealed class ShoppingCart
{
    public Guid ShoppingCartId { get; set; }

    /// <summary>
    /// Stores the Kaleido.Process correlation id that allows the consumer service
    /// to re-enter the same cart process conversation across requests.
    /// </summary>
    public required string ParticipantProcessId { get; set; }

    public ShoppingCartStatus Status { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset UpdatedOn { get; set; }

    public ICollection<ShoppingCartItem> Items { get; set; }
        = [];

    public Order? Order { get; set; }
}