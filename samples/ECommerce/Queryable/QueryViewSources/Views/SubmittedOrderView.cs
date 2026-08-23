using Kaleido.Samples.ECommerce.Data.Entities;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

public sealed record SubmittedOrderView
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