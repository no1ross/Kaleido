using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Data.QueryContexts;

[QueryContext(
    Name = "products",
    DisplayName = "Products",
    Version = "1.0.0",
    Source = "E-Commerce Catalog")]
public sealed class ProductCatalogQueryContext
{
    [Key]
    public Guid ProductId { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.Contains,
        FilterOperator.StartsWith)]
    [Searchable(
        Priority = 1,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    public string ProductName { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Searchable(
        Priority = 2,
        MatchMode = MatchMode.Exact)]
    [Sortable]
    public string SupplierName { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Sortable]
    public string FamilyName { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Searchable(
        Priority = 3,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    public string ModelName { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.GreaterThan,
        FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan,
        FilterOperator.LessThanOrEqual,
        FilterOperator.Between)]
    [Sortable]
    public double Price { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.GreaterThan,
        FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan,
        FilterOperator.LessThanOrEqual,
        FilterOperator.Between)]
    [Sortable]
    public double Rating { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.GreaterThan,
        FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan,
        FilterOperator.LessThanOrEqual,
        FilterOperator.Between)]
    [Sortable]
    public int ReviewCount { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.GreaterThan,
        FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan,
        FilterOperator.LessThanOrEqual,
        FilterOperator.Between)]
    [Sortable]
    public int AvailableQuantity { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.IsTrue,
        FilterOperator.IsFalse)]
    [Sortable]
    public bool IsActive { get; init; }


    [Filterable(
        FilterOperator.Equals,
        FilterOperator.GreaterThan,
        FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan,
        FilterOperator.LessThanOrEqual,
        FilterOperator.Between)]
    [Sortable]
    public DateTime ReleasedDate { get; init; }
}