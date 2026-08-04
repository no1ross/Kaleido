using Kaleido.Process.Shared.Entities;

namespace Kaleido.Process.Shared.Data;

public sealed class BillingInfo
{
    public Guid BillingInfoId { get; set; }

    public Guid OrderId { get; set; }

    public PaymentMethodType PaymentMethod { get; set; }

    public required string PaymentToken { get; set; }

    public required Address BillingAddress { get; set; }

    public bool Accepted { get; set; }

    public bool Validated { get; set; }

    public decimal? AuthorizedAmount { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset UpdatedOn { get; set; }

    public Order Order { get; set; } = null!;
}
