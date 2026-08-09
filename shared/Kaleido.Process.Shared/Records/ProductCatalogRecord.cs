using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Records;

[QueryableRecord(
    Name = "products",
    DisplayName = "Products",
    Version = "1.0.0",
    Source = "E-Commerce Catalog")]
[Pageable(25, 250)]
public sealed class ProductCatalogRecord
{
    [Key]
    [Filterable(
        FilterOperator.Equals,
        FilterOperator.In)]
    public Guid ProductId { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.Contains,
        FilterOperator.StartsWith)]
    [Searchable(
        1,
        MatchMode.Exact,
        MatchMode.StartsWith,
        MatchMode.Contains)]
    [Sortable]
    public string ProductName { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Searchable(
        2,
        MatchMode.Exact,
        MatchMode.Contains)]
    [Sortable]
    public string SupplierName { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Searchable(
        3,
        MatchMode.Exact,
        MatchMode.Contains)]
    [Sortable]
    public string CategoryName { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Sortable]
    public string CategoryPath { get; init; } = string.Empty;

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
}