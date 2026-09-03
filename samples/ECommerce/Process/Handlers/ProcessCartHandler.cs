using Kaleido.Process.Execution;
using Kaleido.Process;

using Kaleido.Samples.ECommerce.Data;
using Kaleido.Samples.ECommerce.Data.Entities;

using Kaleido.Samples.ECommerce.Process.Steps;

using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.ECommerce.Process.Handlers;

internal sealed class ProcessCartHandler(
    ECommerceDbContext dbContext)
    : IProcessStepHandler<ProcessCartStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        ProcessCartStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        var customer =
            await dbContext.Customers
                .FirstOrDefaultAsync(
                    x => x.CustomerId ==
                         step.CustomerId,
                    cancellationToken);

        if (customer is null)
        {
            return ProcessStepHandlerResult.Failure(
                ProcessStpMessages.CustomerNotFound(
                    step.CustomerId));
        }

        if (!customer.IsActive)
        {
            return ProcessStepHandlerResult.Failure(
                ProcessStpMessages.CustomerInactive(
                    step.CustomerId));
        }

        var shoppingCart =
            await dbContext.ShoppingCarts
                .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(
                    x => x.ShoppingCartId ==
                         step.ShoppingCartId,
                    cancellationToken);

        if (shoppingCart is null)
        {
            return ProcessStepHandlerResult.Failure(
                ProcessStpMessages.ShoppingCartNotFound(
                    step.ShoppingCartId));
        }

        if (shoppingCart.CustomerId !=
            step.CustomerId)
        {
            return ProcessStepHandlerResult.Failure(
                ProcessStpMessages.ShoppingCartCustomerMismatch(
                    step.ShoppingCartId,
                    step.CustomerId));
        }

        if (shoppingCart.ProcessId !=
            context.ProcessId)
        {
            return ProcessStepHandlerResult.Failure(
                ProcessStpMessages.ShoppingCartProcessMismatch(
                    step.ShoppingCartId,
                    context.ProcessId));
        }

        if (shoppingCart.Items.Count == 0)
        {
            return ProcessStepHandlerResult.Failure(
                ProcessStpMessages.ShoppingCartEmpty(
                    step.ShoppingCartId));
        }

        var order =
            await dbContext.Orders
                .Include(x => x.Items).ThenInclude(x => x.Product)
                .Include(x => x.StatusHistory)
                .FirstOrDefaultAsync(
                    x =>
                        x.ProcessId ==
                            context.ProcessId &&
                        x.CustomerId ==
                            step.CustomerId &&
                        x.Status ==
                            OrderStatus.Started,
                    cancellationToken);

        var isNewOrder =
            order is null;

        if (order is null)
        {
            order =
                new Order
                {
                    OrderId =
                        Guid.NewGuid(),

                    CustomerId =
                        step.CustomerId,

                    ShoppingCartId = 
                        shoppingCart.ShoppingCartId,

                    ProcessId =
                        context.ProcessId,

                    Status =
                        OrderStatus.Started,

                    CreatedUtc =
                        DateTime.UtcNow,

                    OrderNumber = null
                };

            var statusHistory =
                new OrderStatusHistory
                {
                    OrderStatusHistoryId =
                        Guid.NewGuid(),

                    OrderId =
                        order.OrderId,

                    FromStatus =
                        OrderStatus.Unknown,

                    ToStatus =
                        OrderStatus.Started,

                    ChangedUtc =
                        DateTime.UtcNow,

                    Reason =
                        "Shopping cart processed."
                };

            dbContext.Orders.Add(order);
            dbContext.OrderStatusHistories.Add(statusHistory);
        }
        else
        {
            dbContext.OrderItems.RemoveRange(order.Items);
        }

        foreach (var cartItem in shoppingCart.Items)
        {
            var orderItem =
                new OrderItem
                {
                    OrderItemId =
                        Guid.NewGuid(),

                    OrderId =
                        order.OrderId,

                    ProductId =
                        cartItem.ProductId,

                    ProductName =
                        cartItem.Product.Name,

                    ProductSku =
                        cartItem.Product.Sku,

                    Quantity =
                        cartItem.Quantity,

                    UnitPrice =
                        cartItem.UnitPrice
                };

            dbContext.OrderItems.Add(orderItem);
        }

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (Exception ex)
        {

            throw;
        }

        return ProcessStepHandlerResult.Success(
            isNewOrder
                ? ProcessStpMessages.OrderStarted(
                    order.OrderId)
                : ProcessStpMessages.OrderUpdatedFromCart(
                    order.OrderId));
    }
}