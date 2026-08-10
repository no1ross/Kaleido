using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Records;
using Kaleido.Samples.ECommerce.Data;
using Kaleido.Samples.ECommerce.Records;
using System.ComponentModel.DataAnnotations;

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

                CategoryLevel = category.Level,

                Price = (double)product.Price,

                Rating = product.Rating,

                ReviewCount = product.ReviewCount,

                AvailableQuantity = inventory.AvailableQuantity,

                IsActive = product.IsActive
            };
    }
}

internal sealed class ProductCatalogRecordView
    : IRecordView<ProductCatalogRecord, ProductCatalogView>
{
    public IQueryable<ProductCatalogView> CreateView(IQueryable<ProductCatalogRecord> query, RecordExecutionContext executionContext)
    {
        return query.Select(record => new ProductCatalogView
            {
                ProductId = record.ProductId,

                ProductName = record.ProductName,

                SupplierName = record.SupplierName,

                CategoryName = record.CategoryName,

                CategoryPath = record.CategoryPath,

                Price = (double)record.Price,

                Rating = record.Rating,

                ReviewCount = record.ReviewCount,

                AvailableQuantity = record.AvailableQuantity,

                IsActive = record.IsActive
            });
    }
}

internal sealed class CategoryCatalogViewSource
    : IRecordView<ProductCatalogRecord, CategoryCatalogView>
{
    public IQueryable<CategoryCatalogView> CreateView(
        IQueryable<ProductCatalogRecord> query, RecordExecutionContext executionContext)
    {
        return query
            .GroupBy(x =>
                new
                {
                    x.CategoryName,
                    x.CategoryPath,
                    x.CategoryLevel
                })
            .Select(x =>
                new CategoryCatalogView
                {
                    CategoryName =
                        x.Key.CategoryName,

                    CategoryPath =
                        x.Key.CategoryPath,

                    Level =
                        x.Key.CategoryLevel,

                    ProductCount =
                        x.Count()
                })
            .OrderBy(x =>
                x.CategoryName);
    }
}

[RecordView(
    Name = "products",
    DisplayName = "Products",
    Version = "1.0.0",
    Description = "Product catalog results.",
    ApplyPaging = true,
    ApplySorting = true)]
public sealed class ProductCatalogView
{
    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string SupplierName { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public string CategoryPath { get; init; } = string.Empty;

    public double Price { get; init; }

    public double Rating { get; init; }

    public int ReviewCount { get; init; }

    public int AvailableQuantity { get; init; }

    public bool IsActive { get; init; }
}

[RecordView(
    Name = "categories",
    DisplayName = "Categories",
    Version = "1.0.0",
    Description = "Category navigation results for the current catalog context.",
    ApplyPaging = false,
    ApplySorting = false)]
public sealed class CategoryCatalogView
{
    public string CategoryName
    {
        get;
        init;
    }
        = string.Empty;

    public string CategoryPath
    {
        get;
        init;
    }
        = string.Empty;

    public int Level
    {
        get;
        init;
    }

    public int ProductCount
    {
        get;
        init;
    }
}

