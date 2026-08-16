using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.ECommerce.Data.QueryContexts;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources;

[QueryView(
    Name = "product-list",
    DisplayName = "Product List",
    Version = "1.0.0",
    Description = "Product catalog results.",
    DefaultSortField = nameof(ProductCatalogQueryContext.ProductName))]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class ProductListQueryViewSource
    : IQueryViewSource<ProductCatalogQueryContext, ProductCatalogView>
{
    public IQueryable<ProductCatalogView> CreateView(
        IQueryable<ProductCatalogQueryContext> query,
        QueryExecutionContext executionContext)
    {
        return query.Select(record =>
            new ProductCatalogView
            {
                ProductId =
                    record.ProductId,

                ProductName =
                    record.ProductName,

                SupplierName =
                    record.SupplierName,

                FamilyName =
                    record.FamilyName,

                ModelName =
                    record.ModelName,

                Price =
                    record.Price,

                Rating =
                    record.Rating,

                ReviewCount =
                    record.ReviewCount,

                AvailableQuantity =
                    record.AvailableQuantity,

                IsActive =
                    record.IsActive,

                ReleasedDate = record.ReleasedDate
            });
    }
}
