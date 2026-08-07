using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Shared.Data;
using Kaleido.Process.Shared.Responses;
using Kaleido.Process.Shared.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Process.Shared.Handlers;

public sealed class StartOrderHandler(
    ShoppingCartDbContext dbContext)
    : IProcessStepHandler<StartOrderStep, StartOrderResponse>
{
    public async Task<ProcessStepHandlerResult<StartOrderResponse>> ExecuteAsync(
        StartOrderStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        var now =
            DateTimeOffset.UtcNow;

        var shoppingCartId =
            Guid.Parse(step.CartId);

        var correlationId =
            GetCorrelationId(context);

        var cart =
            await dbContext.ShoppingCarts
                .Include(x => x.Items)
                .SingleAsync(
                    x => x.ShoppingCartId == shoppingCartId,
                    cancellationToken);

        var existingOrder =
            await dbContext.Orders
                .SingleOrDefaultAsync(
                    x => x.ShoppingCartId == shoppingCartId,
                    cancellationToken);

        var order =
            existingOrder
            ?? new Order
            {
                OrderId = Guid.NewGuid(),
                ShoppingCartId = shoppingCartId,
                ParticipantProcessId = correlationId,
                MemberId = step.MemberId,
                Status = OrderStatus.Draft,
                Priority = step.Priority,
                ShippingAddress = step.ShippingAddress,
                TermsAccepted = false,
                Submitted = false,
                CreatedOn = now,
                UpdatedOn = now
            };

        if (existingOrder is null)
        {
            dbContext.Orders.Add(order);
        }
        else
        {
            order.MemberId = step.MemberId;
            order.Priority = step.Priority;
            order.ShippingAddress = step.ShippingAddress;
            order.UpdatedOn = now;
        }

        cart.Status = ShoppingCartStatus.ConvertedToOrder;
        cart.UpdatedOn = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        var response =
            new StartOrderResponse
            {
                OrderId = order.OrderId.ToString(),
                CreatedOn = order.CreatedOn,
                Priority = order.Priority,
                Notes = $"Order created from cart {cart.ShoppingCartId}."
            };

        return new ProcessStepHandlerResult<StartOrderResponse>
        {
            Response = response
        };
    }

    private static string GetCorrelationId(
        ProcessStepContext context)
    {
        // KALEIDO_ADAPT:
        // Replace with the actual correlation id property exposed by ProcessStepContext.
        return context.ParticipantProcessId;
    }
}