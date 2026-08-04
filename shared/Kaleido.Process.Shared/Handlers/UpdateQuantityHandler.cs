using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Shared.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Process.Shared.Handlers;

public sealed class UpdateQuantityHandler(
    ShoppingCartDbContext dbContext)
    : IProcessStepHandler<UpdateQuantityStep, UpdateQuantityResponse>
{
    public async Task<ProcessStepHandlerResult<UpdateQuantityResponse>> ExecuteAsync(
        UpdateQuantityStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        var now =
            DateTimeOffset.UtcNow;

        var shoppingCartId =
            Guid.Parse(step.CartId);

        var cart =
            await dbContext.ShoppingCarts
                .Include(x => x.Items)
                .SingleAsync(
                    x => x.ShoppingCartId == shoppingCartId,
                    cancellationToken);

        var item =
            cart.Items.Single(x =>
                string.Equals(
                    x.ItemId,
                    step.ItemId,
                    StringComparison.OrdinalIgnoreCase));

        item.Quantity = step.Quantity;
        item.UpdatedOn = now;

        cart.UpdatedOn = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        var response =
            new UpdateQuantityResponse
            {
                CartId = cart.ShoppingCartId.ToString(),
                ItemId = item.ItemId,
                Quantity = item.Quantity,
                CartTotal = cart.Items.Sum(x => x.Quantity * x.UnitPrice)
            };

        return new ProcessStepHandlerResult<UpdateQuantityResponse>
        {
            Response = response
        };
    }
}