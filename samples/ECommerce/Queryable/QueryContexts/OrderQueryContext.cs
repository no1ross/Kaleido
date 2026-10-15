using System.ComponentModel;
using Kaleido.Queryable;

namespace Kaleido.Samples.ECommerce.Data.QueryContexts;

public sealed record OrderQueryContext : IQueryContext
{
    [Description("The order this item belongs to.")]
    public Guid OrderId
    {
        get;
        init;
    }

    [Description("The customer who placed the order.")]
    public Guid CustomerId
    {
        get;
        init;
    }

    [Description("The shopping cart the order was created from, if any.")]
    public Guid? ShoppingCartId
    {
        get;
        init;
    }

    [Description("The process that created the order.")]
    public Guid ProcessId
    {
        get;
        init;
    }

    [Description("The human-readable order number.")]
    public string OrderNumber
    {
        get;
        init;
    } = string.Empty;

    [Description("The current status of the order.")]
    public OrderStatus Status
    {
        get;
        init;
    }

    [Description("When the order was created (UTC).")]
    public DateTime CreatedUtc
    {
        get;
        init;
    }

    [Description("When the order was submitted (UTC), if it has been.")]
    public DateTime? SubmittedUtc
    {
        get;
        init;
    }

    [Description("When the order was cancelled (UTC), if it has been.")]
    public DateTime? CancelledUtc
    {
        get;
        init;
    }

    [Description("When the order was last updated (UTC).")]
    public DateTime UpdatedUtc
    {
        get;
        init;
    }

    [Description("The unique ID of this order item.")]
    public Guid OrderItemId
    {
        get;
        init;
    }

    [Description("The product ordered on this item.")]
    public Guid ProductId
    {
        get;
        init;
    }

    [Description("The name of the product ordered.")]
    public string ProductName
    {
        get;
        init;
    } = string.Empty;

    [Description("The SKU of the product ordered.")]
    public string ProductSku
    {
        get;
        init;
    } = string.Empty;

    [Description("The supplier of the product.")]
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

    [Description("The description of the product.")]
    public string Description
    {
        get;
        init;
    } = string.Empty;

    [Description("How many units of the product were ordered.")]
    public int Quantity
    {
        get;
        init;
    }

    [Description("The price of one unit of the product.")]
    public decimal UnitPrice
    {
        get;
        init;
    }

    [Description("The total price for this item (quantity times unit price).")]
    public decimal ExtendedPrice
    {
        get;
        init;
    }
}
