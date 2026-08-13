using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.ECommerce.Data;
using Kaleido.Samples.ECommerce.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Sources;

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

                IsActive = product.IsActive
            };
    }
}

[QueryView(
    Name = "product-list",
    DisplayName = "Product List",
    Version = "1.0.0",
    Description = "Product catalog results.")]
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
                    record.IsActive
            });
    }
}


[QueryView(
    Name = "categories",
    DisplayName = "Categories",
    Version = "1.0.0",
    Description = "Category navigation results for the current catalog context.")]
internal sealed class CategoryListQueryViewSource
    : IQueryViewSource<ProductCatalogQueryContext, CategoryCatalogView, ProductByCategoryParameters>
{
    private readonly ECommerceDbContext _dbContext;

    public CategoryListQueryViewSource(
        ECommerceDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    public IQueryable<CategoryCatalogView> CreateView(
        IQueryable<ProductCatalogQueryContext> query,
        QueryExecutionContext executionContext)
    {
        var parameters =
            executionContext
                .TryGetViewParameters<ProductByCategoryParameters>();

        var selectedCategoryPath =
            parameters
                ?.CategoryPath
                ?.Trim('/');

        var hasSelectedCategory =
            !string.IsNullOrWhiteSpace(
                selectedCategoryPath);

        if (hasSelectedCategory)
        {
            query =
                ApplyCategoryFilter(
                    query,
                    selectedCategoryPath!);
        }

        var categoryRowsQuery =
            from product in query

            join assignment in _dbContext.ProductCategoryAssignments
                on product.ProductId equals assignment.ProductId

            join category in _dbContext.ProductCategories
                on assignment.ProductCategoryId equals category.ProductCategoryId

            select new
            {
                product.ProductId,

                CategoryName =
                    category.Name,

                CategoryPath =
                    category.Path,

                CategoryLevel =
                    category.Level
            };

        if (hasSelectedCategory)
        {
            var selectedPath =
                selectedCategoryPath!;

            categoryRowsQuery =
                categoryRowsQuery
                    .Where(x =>
                        x.CategoryPath == selectedPath ||
                        x.CategoryPath.StartsWith(
                            selectedPath + "/"));
        }

        var categoryRows =
            categoryRowsQuery
                .ToList();

        var categoryList =
            new Dictionary<string, CategoryAccumulator>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var row in categoryRows)
        {
            var segments =
                row.CategoryPath.Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries);

            for (var i = 0; i < segments.Length; i++)
            {
                var expandedCategoryPath =
                    string.Join(
                        '/',
                        segments.Take(i + 1));

                if (!categoryList.TryGetValue(
                        expandedCategoryPath,
                        out var category))
                {
                    category =
                        new CategoryAccumulator
                        {
                            CategoryName =
                                segments[i],

                            CategoryPath =
                                expandedCategoryPath,

                            Level =
                                i
                        };

                    categoryList.Add(
                        expandedCategoryPath,
                        category);
                }

                category.ProductIds.Add(
                    row.ProductId!);
            }
        }

        IEnumerable<CategoryAccumulator> result =
            categoryList.Values;

        if (hasSelectedCategory)
        {
            var selectedSegments =
                selectedCategoryPath!
                    .Split(
                        '/',
                        StringSplitOptions.RemoveEmptyEntries);

            var childLevel =
                selectedSegments.Length;

            var selectedPrefix =
                selectedCategoryPath! + "/";

            result =
                result.Where(x =>
                    x.Level == childLevel &&
                    x.CategoryPath.StartsWith(
                        selectedPrefix,
                        StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            result =
                result.Where(x =>
                    x.Level == 0);
        }

        return result
            .Where(x =>
                x.ProductIds.Count > 1)
            .OrderBy(x =>
                x.CategoryPath)
            .Select(x =>
                new CategoryCatalogView
                {
                    CategoryName =
                        x.CategoryName,

                    CategoryPath =
                        x.CategoryPath,

                    Level =
                        x.Level,

                    ProductCount =
                        x.ProductIds.Count
                })
            .AsQueryable();
    }

    private sealed class CategoryAccumulator
    {
        public required string CategoryName { get; init; }

        public required string CategoryPath { get; init; }

        public required int Level { get; init; }

        public HashSet<object> ProductIds { get; } =
            new();
    }

    private IQueryable<ProductCatalogQueryContext> ApplyCategoryFilter(
        IQueryable<ProductCatalogQueryContext> query,
        string categoryPath)
    {
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
                                ))));
    }
}

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

public sealed record CategoryCatalogView
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

public sealed class ProductByCategoryParameters
{
    [Required]
    [Description("The category path used to filter products.")]
    public required string CategoryPath { get; init; }
}

[QueryView(
    Name = "product-by-category",
    DisplayName = "Products By Category",
    Version = "1.0.0",
    Description = "Product catalog results.")]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class ProductByCategoryQueryView
    : IQueryViewSource<ProductCatalogQueryContext, ProductCatalogView, ProductByCategoryParameters>
{
    private readonly ECommerceDbContext _dbContext;

    public ProductByCategoryQueryView(
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
                        record.IsActive
                });

    }
}