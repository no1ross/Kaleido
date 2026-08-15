using Kaleido.Queryable.Query;

using Kaleido.Samples.ECommerce.Data.QueryContexts;

using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.ECommerce.Data.QueryContextSources;

internal sealed class OrderQueryContextSource(
    ECommerceDbContext dbContext)
    : IQueryContextSource<OrderQueryContext>
{
    public IQueryable<OrderQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.OrderItems
            .AsNoTracking()
            .Select(orderItem =>
                new OrderQueryContext
                {
                    OrderId =
                        orderItem.OrderId,

                    CustomerId =
                        orderItem.Order.CustomerId,

                    ShoppingCartId =
                        orderItem.Order.ShoppingCartId,

                    ParticipantProcessId =
                        orderItem.Order.ParticipantProcessId,

                    OrderNumber =
                        orderItem.Order.OrderNumber,

                    Status =
                        orderItem.Order.Status,

                    CreatedUtc =
                        orderItem.Order.CreatedUtc,

                    SubmittedUtc =
                        orderItem.Order.SubmittedUtc,

                    CancelledUtc =
                        orderItem.Order.CancelledUtc,

                    UpdatedUtc =
                        orderItem.Order.UpdatedUtc,

                    OrderItemId =
                        orderItem.OrderItemId,

                    ProductId =
                        orderItem.ProductId,

                    ProductName =
                        orderItem.ProductName,

                    ProductSku =
                        orderItem.ProductSku,

                    SupplierName =
                        orderItem.Product.Supplier.Name,

                    FamilyName =
                        orderItem.Product.FamilyName,

                    ModelName =
                        orderItem.Product.ModelName,

                    Description =
                        orderItem.Product.Description,

                    Quantity =
                        orderItem.Quantity,

                    UnitPrice =
                        orderItem.UnitPrice,

                    ExtendedPrice =
                        orderItem.Quantity *
                        orderItem.UnitPrice
                });
    }
}