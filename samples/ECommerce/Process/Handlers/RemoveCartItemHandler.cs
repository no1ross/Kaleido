using Kaleido.Process.Participant.Execution;
using Kaleido.Samples.ECommerce.Data;
using Kaleido.Samples.ECommerce.Process.Steps;
using Kaleido.Samples.ECommerce.Process.Responses;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.ECommerce.Process.Handlers;

public sealed class RemoveCartItemHandler(
    ECommerceDbContext dbContext)
    : IProcessStepHandler<RemoveCartItemStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        RemoveCartItemStep step,
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

        var productName =
            cartItem.Product.Name;

        dbContext.ShoppingCartItems.Remove(cartItem);

        shoppingCart.UpdatedUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return ProcessStepHandlerResult.Success(
            ShoppingCartMessages.ItemRemovedFromCart(
                productName));
    }
}
