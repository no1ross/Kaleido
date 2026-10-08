using Kaleido.Queryable;

using Kaleido.Samples.ECommerce.Data.QueryContexts;

using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.ECommerce.Data.QueryContextSources;

[QuerySource(
    Version = "1.0",
    DisplayName = "Orders",
    Description = "Customer orders with their status and totals.",
    Source = "E-Commerce Orders")]
internal sealed class OrderQueryContextSource(
    ECommerceDbContext dbContext)
    : IQuerySource<OrderQueryContext>
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

                    ProcessId =
                        orderItem.Order.ProcessId,

                    OrderNumber =
                        orderItem.Order.OrderNumber ??
                        string.Empty,

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