using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;

namespace Kaleido.Samples.ECommerce.Data.QueryContexts;

public sealed class ProductCatalogQueryContext : IQueryContext
{
    [Key]
    [Description("The unique ID of the product.")]
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
    [Description("The name of the product.")]
    public string ProductName { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Searchable(
        Priority = 2,
        MatchMode = MatchMode.Exact)]
    [Sortable]
    [Description("The supplier of the product.")]
    public string SupplierName { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Sortable]
    [Description("The product family the product belongs to.")]
    public string FamilyName { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.In)]
    [Searchable(
        Priority = 3,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    [Description("The product's model name.")]
    public string ModelName { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.GreaterThan,
        FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan,
        FilterOperator.LessThanOrEqual,
        FilterOperator.Between)]
    [Sortable]
    [Description("The price of one unit of the product.")]
    public double Price { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.GreaterThan,
        FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan,
        FilterOperator.LessThanOrEqual,
        FilterOperator.Between)]
    [Sortable]
    [Description("The product's average customer rating.")]
    public double Rating { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.GreaterThan,
        FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan,
        FilterOperator.LessThanOrEqual,
        FilterOperator.Between)]
    [Sortable]
    [Description("How many customer reviews the product has.")]
    public int ReviewCount { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.GreaterThan,
        FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan,
        FilterOperator.LessThanOrEqual,
        FilterOperator.Between)]
    [Sortable]
    [Description("How many units are in stock.")]
    public int AvailableQuantity { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.IsTrue,
        FilterOperator.IsFalse)]
    [Sortable]
    [Description("Whether the product is available for sale.")]
    public bool IsActive { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.GreaterThan,
        FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan,
        FilterOperator.LessThanOrEqual,
        FilterOperator.Between)]
    [Sortable]
    [Description("When the product was released.")]
    public DateTime ReleasedDate { get; init; }
}
