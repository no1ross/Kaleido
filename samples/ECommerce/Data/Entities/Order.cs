using Kaleido.Samples.ECommerce.Data;

namespace Kaleido.Samples.ECommerce.Data.Entities;

public sealed class Order
{
    public Guid OrderId { get; set; }

    public Guid CustomerId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public OrderStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? SubmittedUtc { get; set; }

    public DateTime? CancelledUtc { get; set; }

    public Customer Customer { get; set; } = null!;

    public BillingInfo? BillingInfo { get; set; }

    public ICollection<OrderItem> Items { get; set; }
        = new List<OrderItem>();

    public ICollection<OrderStatusHistory> StatusHistory { get; set; }
        = new List<OrderStatusHistory>();

    public OrderCancellation? Cancellation { get; set; }
}