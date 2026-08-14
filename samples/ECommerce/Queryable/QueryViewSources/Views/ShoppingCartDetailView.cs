namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

public sealed record ShoppingCartDetailView
{
    public Guid ShoppingCartId
    {
        get;
        init;
    }

    public Guid ShoppingCartItemId
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
