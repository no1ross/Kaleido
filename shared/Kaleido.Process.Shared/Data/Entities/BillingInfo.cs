namespace Kaleido.Samples.ECommerce.Data.Entities;

public sealed class BillingInfo
{
    public Guid BillingInfoId { get; set; }

    public Guid OrderId { get; set; }

    public string CardholderName { get; set; } = string.Empty;

    public string CardLastFourDigits { get; set; } = string.Empty;

    public string BillingAddress1 { get; set; } = string.Empty;

    public string? BillingAddress2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public Order Order { get; set; } = null!;
}
