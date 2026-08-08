//using Kaleido.Process.Participant.Execution;
//using Kaleido.Process.Shared.Responses;
//using Kaleido.Process.Shared.Steps;
//using Microsoft.EntityFrameworkCore;

//namespace Kaleido.Process.Shared.Handlers;

//public sealed class RemoveItemHandler(
//    ShoppingCartDbContext dbContext)
//    : IProcessStepHandler<RemoveItemStep, RemoveItemResponse>
//{
//    public async Task<ProcessStepHandlerResult<RemoveItemResponse>> ExecuteAsync(
//        RemoveItemStep step,
//        ProcessStepContext context,
//        CancellationToken cancellationToken = default)
//    {
//        var now =
//            DateTimeOffset.UtcNow;

//        var shoppingCartId =
//            Guid.Parse(step.CartId);

//        var cart =
//            await dbContext.ShoppingCarts
//                .Include(x => x.Items)
//                .SingleAsync(
//                    x => x.ShoppingCartId == shoppingCartId,
//                    cancellationToken);

//        var item =
//            cart.Items.Single(x =>
//                string.Equals(
//                    x.ItemId,
//                    step.ItemId,
//                    StringComparison.OrdinalIgnoreCase));

//        dbContext.ShoppingCartItems.Remove(item);

//        cart.UpdatedOn = now;

//        await dbContext.SaveChangesAsync(cancellationToken);

//        var remainingItems =
//            await dbContext.ShoppingCartItems
//                .Where(x => x.ShoppingCartId == shoppingCartId)
//                .SumAsync(
//                    x => x.Quantity,
//                    cancellationToken);

//        var response =
//            new RemoveItemResponse
//            {
//                CartId = cart.ShoppingCartId.ToString(),
//                ItemId = step.ItemId,
//                Removed = true,
//                RemainingItems = remainingItems
//            };

//        return new ProcessStepHandlerResult<RemoveItemResponse>
//        {
//            Response = response
//        };
//    }
//}
