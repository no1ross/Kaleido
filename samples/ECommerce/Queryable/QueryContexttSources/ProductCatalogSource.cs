using Kaleido.Queryable.Query;
using Kaleido.Samples.ECommerce.Data.QueryContexts;

namespace Kaleido.Samples.ECommerce.Data.QueryContexttSources;

internal sealed class ProductCatalogContextSource
    : IQueryContextSource<ProductCatalogQueryContext>
{
    private readonly ECommerceDbContext _dbContext;

    public ProductCatalogContextSource(
        ECommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<ProductCatalogQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return
            from product in _dbContext.Products

            join supplier in _dbContext.Suppliers
                on product.SupplierId equals supplier.SupplierId

            join inventory in _dbContext.Inventories
                on product.ProductId equals inventory.ProductId

            select new ProductCatalogQueryContext
            {
                ProductId = product.ProductId,

                ProductName = product.Name,

                SupplierName = supplier.Name,

                FamilyName = product.FamilyName,

                ModelName = product.ModelName,

                Price = (double)product.Price,

                Rating = product.Rating,

                ReviewCount = product.ReviewCount,

                AvailableQuantity = inventory.AvailableQuantity,

                IsActive = product.IsActive,
                
                ReleasedDate = product.ReleasedUtc
            };
    }
}
