using Kaleido.Process.Shared.Entities;

namespace Kaleido.Process.Shared.Data;

public sealed class Order
{
    public Guid OrderId { get; set; }

    public Guid ShoppingCartId { get; set; }

    /// <summary>
    /// Stores the Kaleido.Process correlation id that allows the consumer service
    /// to re-enter the same order process conversation across requests.
    /// </summary>
    public required string CorrelationId { get; set; }

    public required string MemberId { get; set; }

    public OrderStatus Status { get; set; }

    public OrderPriority Priority { get; set; }

    public required Address ShippingAddress { get; set; }

    public bool TermsAccepted { get; set; }

    public DateTimeOffset? TermsAcceptedOn { get; set; }

    public bool Submitted { get; set; }

    public string? SubmissionId { get; set; }

    public DateTimeOffset? SubmittedOn { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset UpdatedOn { get; set; }

    public ShoppingCart ShoppingCart { get; set; } = null!;

    public BillingInfo? BillingInfo { get; set; }

    public OrderCancellation? Cancellation { get; set; }
}