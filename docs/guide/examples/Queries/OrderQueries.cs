using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Guide.Examples.GettingStarted;
using Kaleido.Observability;
using Kaleido.Queryable;
using Kaleido.Queryable.Query;

namespace Kaleido.Guide.Examples.Queries;

// snippet: queries-view
// A view: one projection of a source, for one purpose (here, a list), with its own paging.
public sealed record OrderLine
{
    [Description("The order number and what was ordered.")]
    public string Label { get; init; } = string.Empty;
}

[QueryView(
    Version = "1.0.0",
    DisplayName = "Order lines",
    Description = "One line per order, for lists.",
    DefaultSortField = nameof(OrderQueryContext.Number))]
[Pageable(DefaultSize = 10, MaxSize = 50)]
public sealed class OrderLineView : IQueryViewSource<OrderSource, OrderQueryContext, OrderLine>
{
    public IQueryable<OrderLine> CreateView(IQueryable<OrderQueryContext> query, QueryExecutionContext executionContext) =>
        query.Select(x => new OrderLine { Label = "#" + x.Number + " " + x.Item });
}
// end snippet

// snippet: queries-view-parameters
// A view with parameters: the caller supplies them with the query.
public sealed record LargeOrdersParameters : IQueryParameters
{
    [Range(1, 10)]
    [Description("The smallest quantity to include.")]
    public int MinimumQuantity { get; init; } = 5;
}

[QueryView(
    Version = "1.0.0",
    DisplayName = "Large orders",
    Description = "Orders of at least the given quantity.",
    DefaultSortField = nameof(OrderQueryContext.Number))]
[Pageable(DefaultSize = 10, MaxSize = 50)]
public sealed class LargeOrdersView
    : IQueryViewSource<OrderSource, OrderQueryContext, OrderQueryContext, LargeOrdersParameters>
{
    public IQueryable<OrderQueryContext> CreateView(IQueryable<OrderQueryContext> query, QueryExecutionContext executionContext)
    {
        var minimum = executionContext.TryGetViewParameters<LargeOrdersParameters>()?.MinimumQuantity ?? 5;
        return query.Where(x => x.Quantity >= minimum);
    }
}
// end snippet

// snippet: queries-delegated
// A delegated source answers the query itself: for data Kaleido cannot run as LINQ (another
// service's API, a search engine). Kaleido still validates the caller's query against the context.
public sealed record ShippingRateQueryContext : IQueryContext
{
    [Description("The carrier.")]
    [Filterable(FilterOperator.Equals)]
    public string Carrier { get; init; } = string.Empty;
}

public sealed record ShippingRate
{
    [Description("The carrier.")]
    public string Carrier { get; init; } = string.Empty;

    [Description("The price in euros.")]
    public decimal Price { get; init; }
}

public sealed record ShippingRateParameters : IQueryParameters
{
    [Required]
    [Description("The destination country, as an ISO code (NL, DE...).")]
    public string Country { get; init; } = string.Empty;
}

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Shipping rates",
    Description = "Today's rates from the carriers' price service.")]
public sealed class ShippingRateSource
    : IDelegatedQuerySource<ShippingRateQueryContext, ShippingRate, ShippingRateParameters>
{
    public Task<QueryResult<ShippingRate>> ExecuteAsync(
        IQueryRequest<ShippingRateParameters> request,
        CancellationToken cancellationToken)
    {
        // In a real service: call the price service with the request's filter and paging.
        ShippingRate[] rates = request.ViewParameters?.Country == "NL"
            ? [new() { Carrier = "post", Price = 4.95m }, new() { Carrier = "courier", Price = 9.50m }]
            : [new() { Carrier = "post", Price = 12.00m }];

        return Task.FromResult(new QueryResult<ShippingRate>(rates.Length, 0, rates.Length, rates));
    }
}
// end snippet

// snippet: queries-caller
// Record-level access is the source's job: this one returns only the calling customer's invoices.
public sealed record InvoiceQueryContext : IQueryContext
{
    [Description("The invoice number.")]
    [Sortable]
    public int Number { get; init; }

    [Description("The customer the invoice is for.")]
    public string Customer { get; init; } = string.Empty;

    [Description("The amount in euros.")]
    public decimal Amount { get; init; }
}

[QuerySource(
    Version = "1.0.0",
    DisplayName = "My invoices",
    Description = "The calling customer's invoices.",
    DefaultSortField = nameof(InvoiceQueryContext.Number))]
[Pageable(DefaultSize = 20, MaxSize = 100)]
public sealed class MyInvoicesSource(IKaleidoCorrelationContextAccessor correlation) : IQuerySource<InvoiceQueryContext>
{
    private static readonly InvoiceQueryContext[] Invoices =
    [
        new() { Number = 1, Customer = "ann", Amount = 20m },
        new() { Number = 2, Customer = "ben", Amount = 35m },
        new() { Number = 3, Customer = "ann", Amount = 12m },
    ];

    public IQueryable<InvoiceQueryContext> CreateQuery(QueryExecutionContext executionContext)
    {
        // The caller comes from the authenticated principal, never from a header or the query.
        var caller = correlation.Current.CallerName;
        return Invoices.Where(x => x.Customer == caller).AsQueryable();
    }
}
// end snippet
