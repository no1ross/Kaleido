using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.ECommerce.Data.QueryContexts;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources;

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
