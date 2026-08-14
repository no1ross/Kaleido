namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

public sealed class ProductCatalogView
{
    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string SupplierName { get; init; } = string.Empty;

    public string FamilyName { get; init; } = string.Empty;

    public string ModelName { get; init; } = string.Empty;

    public double Price { get; init; }

    public double Rating { get; init; }

    public int ReviewCount { get; init; }

    public int AvailableQuantity { get; init; }

    public bool IsActive { get; init; }
}
