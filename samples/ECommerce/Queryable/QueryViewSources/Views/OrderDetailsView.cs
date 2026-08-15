namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

public sealed record OrderDetailsView
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

    public string OrderNumber
    {
        get;
        init;
    } = string.Empty;

    public OrderStatus Status
    {
        get;
        init;
    } = OrderStatus.Unknown;

    public DateTime CreatedUtc
    {
        get;
        init;
    }

    public DateTime UpdatedUtc
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