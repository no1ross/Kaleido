using Kaleido.Queryable.Attributes;

using Kaleido.Samples.ECommerce.Data.Entities;

namespace Kaleido.Samples.ECommerce.Data.QueryContexts;

[QueryContext(
    Name = "orders",
    DisplayName = "Orders",
    Version = "1.0",
    Source = "E-Commerce Orders")]
public sealed record OrderQueryContext
{
    public Guid OrderId
    {
        get;
        init;
    }

    public Guid CustomerId
    {
        get;
        init;
    }

    public Guid? ShoppingCartId
    {
        get;
        init;
    }

    public Guid ProcessId
    {
        get;
        init;
    }

    public string OrderNumber
    {
        get;
        init;
    } = string.Empty;

    public OrderStatus Status
    {
        get;
        init;
    }

    public DateTime CreatedUtc
    {
        get;
        init;
    }

    public DateTime? SubmittedUtc
    {
        get;
        init;
    }

    public DateTime? CancelledUtc
    {
        get;
        init;
    }

    public DateTime UpdatedUtc
    {
        get;
        init;
    }

    public Guid OrderItemId
    {
        get;
        init;
    }

    public Guid ProductId
    {
        get;
        init;
    }

    public string ProductName
    {
        get;
        init;
    } = string.Empty;

    public string ProductSku
    {
        get;
        init;
    } = string.Empty;

    public string SupplierName
    {
        get;
        init;
    } = string.Empty;

    public string FamilyName
    {
        get;
        init;
    } = string.Empty;

    public string ModelName
    {
        get;
        init;
    } = string.Empty;

    public string Description
    {
        get;
        init;
    } = string.Empty;

    public int Quantity
    {
        get;
        init;
    }

    public decimal UnitPrice
    {
        get;
        init;
    }

    public decimal ExtendedPrice
    {
        get;
        init;
    }
}