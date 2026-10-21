using System.ComponentModel;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

public sealed record ShoppingCartDetailView
{
    [Description("The unique ID of the shopping cart this item belongs to (the cart for the given process, or else the customer's cart).")]
    public Guid ShoppingCartId
    {
        get;
        init;
    }

    [Description("The unique ID of this cart item (one row is returned per item).")]
    public Guid ShoppingCartItemId
    {
        get;
        init;
    }

    [Description("The unique ID of the product in this cart item.")]
    public Guid ProductId
    {
        get;
        init;
    }

    [Description("The current catalog name of the product.")]
    public string ProductName
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

    [Description("The catalog description of the product; empty if the product has none.")]
    public string Description
    {
        get;
        init;
    } = string.Empty;

    [Description("The number of units of the product in the cart.")]
    public int Quantity
    {
        get;
        init;
    }

    [Description("The price of one unit, captured from the catalog price when the item was added to the cart.")]
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
