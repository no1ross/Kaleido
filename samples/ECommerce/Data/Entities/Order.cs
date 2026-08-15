using Kaleido.Samples.ECommerce.Data;

namespace Kaleido.Samples.ECommerce.Data.Entities;

public sealed class Order
{
    public Guid OrderId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid ShoppingCartId { get; set; }

    public Guid ParticipantProcessId { get; set; }

    public string? OrderNumber { get; set; }

    public OrderStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? SubmittedUtc { get; set; }

    public DateTime? CancelledUtc { get; set; }

    public Customer Customer { get; set; } = null!;

    public ShoppingCart ShoppingCart { get; set; } = null!;

    public BillingInfo? BillingInfo { get; set; }

    public ICollection<OrderItem> Items { get; set; }
        = new List<OrderItem>();

    public ICollection<OrderStatusHistory> StatusHistory { get; set; }
        = new List<OrderStatusHistory>();

    public OrderCancellation? Cancellation { get; set; }
    public DateTime UpdatedUtc { get; set; }
}