using Kaleido.Process.Shared.Handlers;
using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.ECommerce.Data;
using Kaleido.Samples.ECommerce.Data.Entities;
using Kaleido.Samples.ECommerce.Sources;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Records;

[QueryContext(
    Name = "shopping-carts",
    DisplayName = "Shopping Carts",
    Version = "1.0.0",
    Source = "E-Commerce Catalog")]
public sealed class ShoppingCartQueryContext
{
    [Key]
    public Guid ShoppingCartId { get; init; }
    public Guid? CustomerId { get; init; } = null;
    public Guid ParticipantProcessId { get; init; }
    public Guid ProductId { get; init; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 0;
    public decimal Price { get; set; } = decimal.Zero;
}

[QueryView(
    Name = "shopping-cart-summary",
    DisplayName = "Shopping Cart Summary",
    Version = "1.0.0",
    Description = "Summary of items in the shopping cart.")]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class ShoppingCartSummaryQueryView
    : IQueryViewSource<ShoppingCartQueryContext, ShoppingCartSummaryView, ShoppingCartSummaryViewParameters>
{
    public IQueryable<ShoppingCartSummaryView> CreateView(
        IQueryable<ShoppingCartQueryContext> query,
        QueryExecutionContext executionContext)
    {
        var summary =
            new ShoppingCartSummaryView
            {
                ItemCount =
                    query.Sum(x => x.Quantity)
            };

        return new[]
        {
            summary
        }.AsQueryable();
    }
}


public sealed record ShoppingCartSummaryView
{
    public required int ItemCount
    {
        get;
        init;
    }
}

public sealed record ShoppingCartSummaryViewParameters
{
    public required Guid ParticipantProcessId
    {
        get;
        init;
    }
}

internal sealed class ShoppingContextSource
    : IQueryContextSource<ShoppingCartQueryContext>
{
    public IQueryable<ShoppingCartQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        var parameters =
            executionContext
                .TryGetViewParameters<ShoppingCartSummaryViewParameters>();

        if (parameters is null)
        {
            return Enumerable
                .Empty<ShoppingCartQueryContext>()
                .AsQueryable();
        }

        var participantProcessId =
                parameters.ParticipantProcessId;

        var cart =
            TempCart.GetCart(
                participantProcessId);

        if (cart is null)
        {
            return Enumerable
                .Empty<ShoppingCartQueryContext>()
                .AsQueryable();
        }

        var rows =
            cart.Items
                .Select(item =>
                    new ShoppingCartQueryContext
                    {
                        ShoppingCartId =
                            cart.ShoppingCartId,

                        CustomerId =
                            cart.CustomerId,

                        ParticipantProcessId =
                            participantProcessId,

                        Name = "Product Name",

                        Description = "Product Description",

                        Quantity =
                            item.Quantity,

                        Price = 10.0m
                    });

        return rows.AsQueryable();
    }
}
