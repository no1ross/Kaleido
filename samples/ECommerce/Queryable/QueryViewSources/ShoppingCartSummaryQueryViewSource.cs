using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.ECommerce.Data.QueryContexts;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources;

[QueryView(
    Name = "shopping-cart-summary",
    DisplayName = "Shopping Cart Summary",
    Version = "1.0.0",
    Description = "Summary of items in the shopping cart.")]
internal sealed class ShoppingCartSummaryQueryViewSource
    : IQueryViewSource<
        ShoppingCartQueryContext,
        ShoppingCartSummaryView,
        ShoppingCartViewParameters>
{
    public IQueryable<ShoppingCartSummaryView> CreateView(
        IQueryable<ShoppingCartQueryContext> query,
        QueryExecutionContext executionContext)
    {
        var parameters =
            executionContext.TryGetViewParameters<ShoppingCartViewParameters>();

        if (parameters is null)
            parameters = new ShoppingCartViewParameters();

        if (parameters.ProcessId is null &&
            parameters.CustomerId is null)
        {
            return CreateEmptySummary();
        }

        IQueryable<ShoppingCartQueryContext>? cartQuery = null;

        if (parameters.ProcessId is not null)
        {
            cartQuery =
                query.Where(x =>
                    x.ProcessId ==
                    parameters.ProcessId);

            if (!cartQuery.Any())
            {
                cartQuery = null;
            }
        }

        if (cartQuery is null &&
            parameters.CustomerId is not null)
        {
            cartQuery =
                query.Where(x =>
                    x.CustomerId ==
                    parameters.CustomerId);

            if (!cartQuery.Any())
            {
                cartQuery = null;
            }
        }

        if (cartQuery is null)
        {
            return CreateEmptySummary();
        }

        cartQuery =
            cartQuery.Where(
                x => x.IsActive);

        var rows =
            cartQuery.ToList();

        if (rows.Count == 0)
        {
            return CreateEmptySummary();
        }

        var firstRow =
            rows.First();

        var summary =
            new ShoppingCartSummaryView
            {
                ProcessId =
                    firstRow.ProcessId,

                ShoppingCartId =
                    firstRow.ShoppingCartId,

                CustomerId = firstRow.CustomerId,

                ItemCount =
                    rows.Sum(x => x.Quantity),

                TotalPrice =
                    rows.Sum(x =>
                        x.Quantity * x.UnitPrice)
            };

        return new[]
        {
            summary
        }.AsQueryable();
    }

    private static IQueryable<ShoppingCartSummaryView> CreateEmptySummary()
    {
        return new[]
        {
            new ShoppingCartSummaryView()
        }.AsQueryable();
    }
}