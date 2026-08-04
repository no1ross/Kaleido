using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Shared.Data;
using Kaleido.Process.Shared.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Process.Shared.Handlers;

public sealed class AddItemToCartHandler(
    ShoppingCartDbContext dbContext)
    : IProcessStepHandler<AddItemToCartStep, AddItemToCartResponse>
{
    public async Task<ProcessStepHandlerResult<AddItemToCartResponse>> ExecuteAsync(
        AddItemToCartStep step,
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
                .SingleOrDefaultAsync(
                    x => x.ShoppingCartId == shoppingCartId,
                    cancellationToken);

        if (cart is null)
        {
            cart =
                new ShoppingCart
                {
                    ShoppingCartId = shoppingCartId,
                    ParticipantProcessId = correlationId,
                    Status = ShoppingCartStatus.Active,
                    CreatedOn = now,
                    UpdatedOn = now
                };

            dbContext.ShoppingCarts.Add(cart);
        }

        foreach (var itemRequest in step.Items)
        {
            var existingItem =
                cart.Items.SingleOrDefault(x =>
                    string.Equals(
                        x.ItemId,
                        itemRequest.ItemId,
                        StringComparison.OrdinalIgnoreCase));

            if (existingItem is null)
            {
                cart.Items.Add(
                    new ShoppingCartItem
                    {
                        ShoppingCartItemId = Guid.NewGuid(),
                        ShoppingCartId = cart.ShoppingCartId,
                        ItemId = itemRequest.ItemId,
                        Description = itemRequest.Description,
                        ItemType = itemRequest.ItemType,
                        Quantity = itemRequest.Quantity,
                        UnitPrice = itemRequest.UnitPrice,
                        CreatedOn = now,
                        UpdatedOn = now
                    });
            }
            else
            {
                existingItem.Quantity += itemRequest.Quantity;
                existingItem.Description = itemRequest.Description;
                existingItem.ItemType = itemRequest.ItemType;
                existingItem.UnitPrice = itemRequest.UnitPrice;
                existingItem.UpdatedOn = now;
            }
        }

        cart.UpdatedOn = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        var response =
            new AddItemToCartResponse
            {
                CartId = cart.ShoppingCartId.ToString(),
                ItemCount = cart.Items.Sum(x => x.Quantity),
                CartTotal = cart.Items.Sum(x => x.Quantity * x.UnitPrice),
                LastUpdated = cart.UpdatedOn
            };

        return new ProcessStepHandlerResult<AddItemToCartResponse>
        {
            Response = response
        };
    }

    private static string GetCorrelationId(
        ProcessStepContext context)
    {
        // KALEIDO_ADAPT:
        // The handler needs the runtime correlation id so the consumer database
        // can map its aggregate back to the Kaleido.Process conversation.
        // Replace this with the actual property once ProcessStepContext exposes it.
        return context.ParticipantProcessId;
    }
}