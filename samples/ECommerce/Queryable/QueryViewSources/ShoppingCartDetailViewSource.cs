using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.ECommerce.Data.QueryContexts;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources;

[QueryView(
    Name = "shopping-cart-detail",
    DisplayName = "Shopping Cart",
    Version = "1.0.0",
    Description = "Shopping cart detail items.")]
internal sealed class ShoppingCartDetailViewSource
    : IQueryViewSource<
        ShoppingCartQueryContext,
        ShoppingCartDetailView,
        ShoppingCartSummaryViewParameters>
{
    public IQueryable<ShoppingCartDetailView> CreateView(
        IQueryable<ShoppingCartQueryContext> query,
        QueryExecutionContext executionContext)
    {
        var parameters =
            executionContext
                .TryGetViewParameters<ShoppingCartSummaryViewParameters>();

        return query
            .Where(x =>
                x.ParticipantProcessId ==
                parameters!.ParticipantProcessId)
            .Select(x =>
                new ShoppingCartDetailView
                {
                    ShoppingCartId =
                        x.ShoppingCartId,

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
}
