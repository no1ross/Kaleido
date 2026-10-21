using System.ComponentModel;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

public sealed class ProductCatalogView
{
    [Description("The unique ID of the product.")]
    public Guid ProductId { get; init; }

    [Description("The name of the product.")]
    public string ProductName { get; init; } = string.Empty;

    [Description("The name of the product's supplier.")]
    public string SupplierName { get; init; } = string.Empty;

    [Description("The product family the product belongs to.")]
    public string FamilyName { get; init; } = string.Empty;

    [Description("The product's model name.")]
    public string ModelName { get; init; } = string.Empty;

    [Description("The current catalog price of one unit of the product.")]
    public double Price { get; init; }

    [Description("The product's average customer rating, rounded to one decimal place.")]
    public double Rating { get; init; }

    [Description("The number of customer reviews the product has.")]
    public int ReviewCount { get; init; }

    [Description("The number of units currently available in inventory.")]
    public int AvailableQuantity { get; init; }

    [Description("Whether the product is available for sale.")]
    public bool IsActive { get; init; }

    [Description("When the product was released, in UTC.")]
    public DateTime ReleasedDate { get; init; }
}
