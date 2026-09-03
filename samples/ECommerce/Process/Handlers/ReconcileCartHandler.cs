using Kaleido.Process;
using Kaleido.Process.Execution;
using Kaleido.Samples.ECommerce.Data;
using Kaleido.Samples.ECommerce.Data.Entities;
using Kaleido.Samples.ECommerce.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.ECommerce.Process.Handlers;

internal sealed class ReconcileCartHandler(
    ECommerceDbContext dbContext)
    : IProcessStepHandler<ReconcileCartStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        ReconcileCartStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        var customer =
            await dbContext.Customers
                .FirstOrDefaultAsync(
                    x => x.CustomerId == step.CustomerId,
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

        var messages =
            new List<ProcessMessage>
            {
                ProcessStpMessages.CustomerSelected(
                    customer.FirstName,
                    customer.LastName)
            };

        //
        // Important:
        // Only anonymous carts associated with the current process
        // are eligible to be transferred or merged.
        //
        // If the current process already has a customer-owned cart,
        // we should not merge it into another customer. That would
        // effectively mix customer-owned state.
        //
        var currentAnonymousCart =
            await dbContext.ShoppingCarts
                .Include(x => x.Items)
                    .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(
                    x =>
                        x.ProcessId ==
                            context.ProcessId &&
                        x.CustomerId == null &&
                        x.IsActive,
                    cancellationToken);

        var customerCart =
            await dbContext.ShoppingCarts
                .Include(x => x.Items)
                    .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(
                    x =>
                        x.CustomerId == customer.CustomerId &&
                        x.IsActive,
                    cancellationToken);

        //
        // No anonymous cart in the current process.
        // No active customer cart.
        //
        // Selecting the customer succeeds, but there is no cart
        // ownership change to perform.
        //
        if (currentAnonymousCart is null &&
            customerCart is null)
        {
            return ProcessStepHandlerResult.Success(
                messages.ToArray());
        }

        //
        // No anonymous cart in the current process.
        // Customer already has an active cart.
        //
        // Do not change the cart's ProcessId.
        // The cart owns that process identity.
        //
        // If the runtime was started without a process id, this
        // indicates we need process recovery before execution,
        // not process id mutation here.
        //
        if (currentAnonymousCart is null &&
            customerCart is not null)
        {
            messages.Add(
                ProcessStpMessages.CustomerCartActivated(
                    customer.FirstName,
                    customer.LastName));

            return ProcessStepHandlerResult.Success(
                messages.ToArray());
        }

        //
        // Current process has an anonymous cart.
        // Customer has no active cart.
        //
        // Transfer the anonymous cart to the selected customer.
        // This does NOT change the ProcessId.
        //
        if (currentAnonymousCart is not null &&
            customerCart is null)
        {
            currentAnonymousCart.CustomerId =
                customer.CustomerId;

            currentAnonymousCart.UpdatedUtc =
                DateTime.UtcNow;

            await dbContext.SaveChangesAsync(
                cancellationToken);

            messages.Add(
                ProcessStpMessages.AnonymousCartTransferredToCustomer(
                    customer.FirstName,
                    customer.LastName));

            return ProcessStepHandlerResult.Success(
                messages.ToArray());
        }

        //
        // Current process has an anonymous cart.
        // Customer also has an active cart.
        //
        // Merge the customer's existing cart into the current
        // anonymous cart, then assign the current cart to the customer.
        //
        // This preserves the current ProcessId and does
        // not mutate the process id on either cart.
        //
        if (currentAnonymousCart is null ||
            customerCart is null)
        {
            throw new InvalidOperationException(
                "Unexpected customer selection cart state.");
        }

        if (currentAnonymousCart.ShoppingCartId ==
            customerCart.ShoppingCartId)
        {
            currentAnonymousCart.CustomerId =
                customer.CustomerId;

            currentAnonymousCart.UpdatedUtc =
                DateTime.UtcNow;

            await dbContext.SaveChangesAsync(
                cancellationToken);

            messages.Add(
                ProcessStpMessages.CustomerCartActivated(
                    customer.FirstName,
                    customer.LastName));

            return ProcessStepHandlerResult.Success(
                messages.ToArray());
        }

        MergeCustomerCartIntoCurrentCart(
            customerCart,
            currentAnonymousCart,
            dbContext);

        currentAnonymousCart.CustomerId =
            customer.CustomerId;

        currentAnonymousCart.UpdatedUtc =
            DateTime.UtcNow;

        dbContext.ShoppingCarts.Remove(
            customerCart);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        messages.Add(
            ProcessStpMessages.CustomerCartMergedIntoCurrentCart(
                customer.FirstName,
                customer.LastName));

        return ProcessStepHandlerResult.Success(
            messages.ToArray());
    }

    private static void MergeCustomerCartIntoCurrentCart(
        ShoppingCart sourceCustomerCart,
        ShoppingCart targetCurrentCart,
        ECommerceDbContext dbContext)
    {
        foreach (var sourceItem in sourceCustomerCart.Items)
        {
            var targetItem =
                targetCurrentCart.Items
                    .FirstOrDefault(
                        x => x.ProductId ==
                             sourceItem.ProductId);

            if (targetItem is not null)
            {
                targetItem.Quantity +=
                    sourceItem.Quantity;

                continue;
            }

            var newItem =
                new ShoppingCartItem
                {
                    ShoppingCartItemId =
                        Guid.NewGuid(),

                    ShoppingCartId =
                        targetCurrentCart.ShoppingCartId,

                    ProductId =
                        sourceItem.ProductId,

                    Quantity =
                        sourceItem.Quantity,

                    UnitPrice =
                        sourceItem.UnitPrice
                };

            dbContext.ShoppingCartItems.Add(
                newItem);
        }
    }
}