namespace Kaleido.Samples.ECommerce.Data.Entities;

public sealed class OrderCancellation
{
    public Guid OrderCancellationId { get; set; }

    public Guid OrderId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime CancelledUtc { get; set; }

    public Order Order { get; set; } = null!;
}