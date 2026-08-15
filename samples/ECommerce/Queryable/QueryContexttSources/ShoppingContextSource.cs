using Kaleido.Queryable.Query;
using Kaleido.Samples.ECommerce.Data.QueryContexts;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;

namespace Kaleido.Samples.ECommerce.Data.QueryContexttSources;

internal sealed class ShoppingCartContextSource(
    ECommerceDbContext dbContext)
    : IQueryContextSource<ShoppingCartQueryContext>
{
    public IQueryable<ShoppingCartQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.ShoppingCartItems
            .Select(item =>
                new ShoppingCartQueryContext
                {
                    ShoppingCartId =
                        item.ShoppingCartId,

                    ShoppingCartItemId =
                        item.ShoppingCartItemId,

                    CustomerId =
                        item.ShoppingCart.CustomerId,

                    ParticipantProcessId =
                        item.ShoppingCart.ParticipantProcessId,

                    ProductId =
                        item.ProductId,

                    ProductName =
                        item.Product.Name,

                    SupplierName =
                        item.Product.Supplier.Name,

                    FamilyName =
                        item.Product.FamilyName,

                    ModelName =
                        item.Product.ModelName,

                    Description =
                        item.Product.Description ?? string.Empty,

                    Quantity =
                        item.Quantity,

                    UnitPrice =
                        item.UnitPrice,

                    IsActive = 
                        item.ShoppingCart.IsActive
                });
    }
}
