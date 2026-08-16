using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.ECommerce.Data.QueryContexts;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources;

[QueryView(
    Name = "product-by-category",
    DisplayName = "Products By Category",
    Version = "1.0.0",
    Description = "Product catalog results.",
    DefaultSortField = nameof(ProductCatalogQueryContext.ProductName))]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class ProductByCategoryQueryViewSource
    : IQueryViewSource<ProductCatalogQueryContext, ProductCatalogView, ProductByCategoryParameters>
{
    private readonly ECommerceDbContext _dbContext;

    public ProductByCategoryQueryViewSource(
        ECommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<ProductCatalogView> CreateView(
        IQueryable<ProductCatalogQueryContext> query,
        QueryExecutionContext executionContext)
    {
        var parameters =
            executionContext
                .TryGetViewParameters<ProductByCategoryParameters>();

        var categoryPath =
            parameters.CategoryPath;

        return query
            .Where(product =>
                _dbContext.ProductCategoryAssignments
                    .Any(assignment =>
                        assignment.ProductId ==
                            product.ProductId &&

                        _dbContext.ProductCategories
                            .Any(category =>
                                category.ProductCategoryId ==
                                    assignment.ProductCategoryId &&

                                (
                                    category.Path ==
                                        categoryPath ||

                                    category.Path.StartsWith(
                                        categoryPath + "/")
                                ))))
            .Select(record =>
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