using Kaleido.Samples.ECommerce;

namespace Kaleido.Samples.ECommerce.Data.Entities;

public sealed class OrderStatusHistory
{
    public Guid OrderStatusHistoryId { get; set; }

    public Guid OrderId { get; set; }

    public OrderStatus FromStatus { get; set; }

    public OrderStatus ToStatus { get; set; }

    public string? Reason { get; set; }

    public DateTime ChangedUtc { get; set; }

    public Order Order { get; set; } = null!;
}