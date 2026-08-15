namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

public sealed record OrderReviewView
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

    public Guid ParticipantProcessId
    {
        get;
        init;
    }

    public OrderStatus Status
    {
        get;
        init;
    } = OrderStatus.Unknown;

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

    public string ProductSku
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