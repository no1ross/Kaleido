using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.ECommerce.Data.QueryContexts;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources;

[QueryView(
    Name = "shopping-cart-detail",
    DisplayName = "Shopping Cart Detail",
    Version = "1.0.0",
    Description = "Shopping cart detail items.")]
internal sealed class ShoppingCartDetailViewSource
    : IQueryViewSource<
        ShoppingCartQueryContext,
        ShoppingCartDetailView,
        ShoppingCartViewParameters>
{
    public IQueryable<ShoppingCartDetailView> CreateView(
        IQueryable<ShoppingCartQueryContext> query,
        QueryExecutionContext executionContext)
    {
        var parameters =
            executionContext.TryGetViewParameters<ShoppingCartViewParameters>();

        if (parameters is null)
            parameters = new ShoppingCartViewParameters();

        if (parameters.ParticipantProcessId is null &&
            parameters.CustomerId is null)
        {
            return CreateEmptyView();
        }

        IQueryable<ShoppingCartQueryContext>? cartQuery = null;

        if (parameters.ParticipantProcessId is not null)
        {
            cartQuery =
                query.Where(x =>
                    x.ParticipantProcessId ==
                    parameters.ParticipantProcessId);

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
            return CreateEmptyView();
        }

        return cartQuery
            .Select(x =>
                new ShoppingCartDetailView
                {
                    ShoppingCartId =
                        x.ShoppingCartId,

                    ShoppingCartItemId = 
                        x.ShoppingCartItemId,

                    ProductId =
                        x.ProductId,

                    ProductName =
                        x.ProductName,

                    SupplierName =
                        x.SupplierName,

                    FamilyName =
                        x.FamilyName,

                    ModelName =
                        x.ModelName,

                    Description =
                        x.Description,

                    Quantity =
                        x.Quantity,

                    UnitPrice =
                        x.UnitPrice,

                    ExtendedPrice =
                        x.Quantity * x.UnitPrice
                });
    }

    private static IQueryable<ShoppingCartDetailView> CreateEmptyView()
    {
        return new[]
        {
            new ShoppingCartDetailView()
        }.AsQueryable();
    }
}
