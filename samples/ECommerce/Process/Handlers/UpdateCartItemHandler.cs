using Kaleido.Process.Participant.Execution;
using Kaleido.Samples.ECommerce.Data;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.ECommerce.Process.Steps;

public sealed class UpdateQuantityHandler(
    ECommerceDbContext dbContext)
    : IProcessStepHandler<UpdateCartItemStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        UpdateCartItemStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
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
                ShoppingCartMessages.ShoppingCartNotFound(
                    step.ShoppingCartId));
        }

        if (shoppingCart.ParticipantProcessId !=
            context.ParticipantProcessId)
        {
            return ProcessStepHandlerResult.Failure(
                ShoppingCartMessages.ShoppingCartProcessMismatch(
                    step.ShoppingCartId,
                    context.ParticipantProcessId));
        }

        var cartItem =
            shoppingCart.Items
                .FirstOrDefault(
                    x => x.ShoppingCartItemId ==
                         step.ShoppingCartItemId);

        if (cartItem is null)
        {
            return ProcessStepHandlerResult.Failure(
                ShoppingCartMessages.ShoppingCartItemNotFound(
                    step.ShoppingCartItemId));
        }

        var previousQuantity =
            cartItem.Quantity;

        cartItem.Quantity =
            step.Quantity;

        shoppingCart.UpdatedUtc =
            DateTime.UtcNow;

        if (step.Quantity <= 0)
        {
            dbContext.ShoppingCartItems.Remove(cartItem);

            await dbContext.SaveChangesAsync(
                cancellationToken);

            return ProcessStepHandlerResult.Success(
                ShoppingCartMessages.ItemRemovedFromCart(
                    cartItem.Product.Name));
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return ProcessStepHandlerResult.Success(
            ShoppingCartMessages.ItemQuantityUpdated(
                cartItem.Product.Name,
                previousQuantity,
                cartItem.Quantity));
    }
}
