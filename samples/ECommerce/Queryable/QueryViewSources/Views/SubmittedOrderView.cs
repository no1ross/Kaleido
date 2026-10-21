using System.ComponentModel;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

public sealed record SubmittedOrderView
{
    [Description("The unique ID of the order this item belongs to.")]
    public Guid OrderId
    {
        get;
        init;
    }

    [Description("The unique ID of the customer who placed the order.")]
    public Guid CustomerId
    {
        get;
        init;
    }

    [Description("The ID of the Kaleido process that created and manages the order.")]
    public Guid ProcessId
    {
        get;
        init;
    }

    [Description("The human-readable order number assigned when the order was submitted (format ORD-yyyyMMddHHmmss-...).")]
    public string OrderNumber
    {
        get;
        init;
    } = string.Empty;

    [Description("The status of the order; always Submitted, because this view returns only submitted orders.")]
    public OrderStatus Status
    {
        get;
        init;
    }

    [Description("When the order was created, in UTC.")]
    public DateTime CreatedUtc
    {
        get;
        init;
    }

    [Description("When the order was submitted, in UTC.")]
    public DateTime? SubmittedUtc
    {
        get;
        init;
    }

    [Description("The unique ID of this order item (one row is returned per item).")]
    public Guid OrderItemId
    {
        get;
        init;
    }

    [Description("The unique ID of the product ordered on this item.")]
    public Guid ProductId
    {
        get;
        init;
    }

    [Description("The name of the product, captured on the order item when the order was created from the cart.")]
    public string ProductName
    {
        get;
        init;
    } = string.Empty;

    [Description("The SKU of the product, captured on the order item when the order was created from the cart.")]
    public string ProductSku
    {
        get;
        init;
    } = string.Empty;

    [Description("The name of the product's supplier.")]
    public string SupplierName
    {
        get;
        init;
    } = string.Empty;

    [Description("The product family the product belongs to.")]
    public string FamilyName
    {
        get;
        init;
    } = string.Empty;

    [Description("The product's model name.")]
    public string ModelName
    {
        get;
        init;
    } = string.Empty;

    [Description("The catalog description of the product.")]
    public string Description
    {
        get;
        init;
    } = string.Empty;

    [Description("The number of units of the product ordered on this item.")]
    public int Quantity
    {
        get;
        init;
    }

    [Description("The price of one unit, copied from the cart item (the catalog price when it was added to the cart).")]
    public decimal UnitPrice
    {
        get;
        init;
    }

    [Description("The total price for this item: Quantity multiplied by UnitPrice.")]
    public decimal ExtendedPrice
    {
        get;
        init;
    }
}
