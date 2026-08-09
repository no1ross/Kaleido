using Kaleido.Queryable.Records;
using Kaleido.Samples.ECommerce.Data;
using Kaleido.Samples.ECommerce.Records;

namespace Kaleido.Samples.ECommerce.Sources;

internal sealed class ProductCatalogSource
    : IRecordSource<ProductCatalogRecord>
{
    private readonly ECommerceDbContext _dbContext;

    public ProductCatalogSource(
        ECommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<ProductCatalogRecord> CreateQuery(
        RecordExecutionContext executionContext)
    {
        return
            from product in _dbContext.Products

            join supplier in _dbContext.Suppliers
                on product.SupplierId equals supplier.SupplierId

            join inventory in _dbContext.Inventories
                on product.ProductId equals inventory.ProductId

            join assignment in _dbContext.ProductCategoryAssignments
                on product.ProductId equals assignment.ProductId

            join category in _dbContext.ProductCategories
                on assignment.ProductCategoryId equals category.ProductCategoryId

            select new ProductCatalogRecord
            {
                ProductId = product.ProductId,

                ProductName = product.Name,

                SupplierName = supplier.Name,

                CategoryName = category.Name,

                CategoryPath = category.Path,

                Price = (double)product.Price,

                Rating = product.Rating,

                ReviewCount = product.ReviewCount,

                AvailableQuantity = inventory.AvailableQuantity,

                IsActive = product.IsActive
            };
    }
}