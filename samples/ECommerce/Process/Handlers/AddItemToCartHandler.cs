using Kaleido.Process;
using Kaleido.Process.Execution;
using Kaleido.Samples.ECommerce.Data;
using Kaleido.Samples.ECommerce.Data.Entities;
using Kaleido.Samples.ECommerce.Process.Responses;
using Kaleido.Samples.ECommerce.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.ECommerce.Process.Handlers;

public sealed class AddItemToCartHandler(
    ECommerceDbContext dbContext)
    : IProcessStepHandler<AddItemToCartStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        AddItemToCartStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ProcessMessage>();

        if (!Guid.TryParse(
                step.ItemId,
                out var productId))
        {
            return ProcessStepHandlerResult.Failure(
                ProcessStpMessages.ProductNotFound(
                    step.ItemId));
        }

        var product = await dbContext.Products
            .FirstOrDefaultAsync(
                x => x.ProductId == productId,
                cancellationToken);

        if (product is null)
        {
            return ProcessStepHandlerResult.Failure(
                ProcessStpMessages.ProductNotFound(
                    step.ItemId));
        }

        var shoppingCart = await dbContext.ShoppingCarts
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.ProcessId ==
                     context.ProcessId,
                cancellationToken);

        if (shoppingCart is null &&
            step.CustomerId.HasValue)
        {
            var activeCart = await dbContext.ShoppingCarts
                .Include(x => x.Items)
                .FirstOrDefaultAsync(
                    x =>
                        x.CustomerId == step.CustomerId.Value &&
                        x.IsActive,
                    cancellationToken);

            if (activeCart is not null)
            {
                if (activeCart.ProcessId !=
                    context.ProcessId)
                {
                    return ProcessStepHandlerResult.Failure(
                        ProcessStpMessages.ActiveCartProcessMismatch(
                            activeCart.ProcessId,
                            context.ProcessId));
                }

                shoppingCart = activeCart;
            }
        }

        var cartCreated =
            shoppingCart is null;

        if (shoppingCart is null)
        {
            shoppingCart = new ShoppingCart
            {
                ShoppingCartId = Guid.NewGuid(),
                CustomerId = step.CustomerId,
                ProcessId = context.ProcessId,
                IsActive = true,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            dbContext.ShoppingCarts.Add(shoppingCart);

            messages.Add(
                ProcessStpMessages.ShoppingCartCreated(
                    shoppingCart.ShoppingCartId));
        }

        var cartItem = shoppingCart.Items
            .FirstOrDefault(
                x => x.ProductId == productId);

        if (cartItem is null)
        {
            cartItem = new ShoppingCartItem
            {
                ShoppingCartItemId = Guid.NewGuid(),
                ShoppingCartId = shoppingCart.ShoppingCartId,
                ProductId = product.ProductId,
                Quantity = step.Quantity,
                UnitPrice = product.Price
            };

            dbContext.ShoppingCartItems.Add(cartItem);
        }
        else
        {
            cartItem.Quantity += step.Quantity;
        }

        shoppingCart.UpdatedUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        messages.Add(ProcessStpMessages.ItemAddedToCart(product.Name, step.Quantity));

        return ProcessStepHandlerResult.Success(messages.ToArray());
    }
}