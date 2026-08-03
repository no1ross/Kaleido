namespace Kaleido.Process.Shared;

public sealed class Product
{
    public Guid Id { get; set; }

    public string Sku { get; set; } = null!;

    public string Name { get; set; } = null!;

    public ProductType Type { get; set; }

    public decimal Price { get; set; }

    public decimal? SalePrice { get; set; }

    public ProductDimensions? Dimensions { get; set; }

    public List<string> Tags { get; set; } = [];
}

public sealed class ProductDimensions
{
    public decimal Height { get; set; }

    public decimal Width { get; set; }

    public decimal Length { get; set; }

    public decimal? Weight { get; set; }
}

public sealed class CartItem
{
    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public Product Product { get; set; } = null!;
}

public sealed class Address
{
    public string Address1 { get; set; } = null!;

    public string? Address2 { get; set; }

    public string City { get; set; } = null!;

    public string State { get; set; } = null!;

    public string PostalCode { get; set; } = null!;

    public string Country { get; set; } = null!;
}

public enum ProductType
{
    Physical,
    Digital,
    Subscription
}

public enum CustomerType
{
    Retail,
    Commercial,
    Government
}

public enum PaymentMethod
{
    CreditCard,
    DebitCard,
    Invoice,
    PurchaseOrder
}

public enum OrderStatus
{
    Draft,
    PendingValidation,
    PendingPayment,
    PendingFulfillment,
    Shipped,
    Complete,
    Failed
}
