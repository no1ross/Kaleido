using Kaleido.Process.Participant.Execution;
using Kaleido.Samples.ECommerce.Data;
using Kaleido.Samples.ECommerce.Data.Entities;
using Kaleido.Samples.ECommerce.Process.Responses;
using Kaleido.Samples.ECommerce.Process.Steps;

namespace Kaleido.Samples.ECommerce.Process.Handlers;

public sealed class AddItemToCartHandler(
    ECommerceDbContext dbContext)
    : IProcessStepHandler<AddItemToCartStep, AddItemToCartResponse>
{
    public async Task<ProcessStepHandlerResult<AddItemToCartResponse>> ExecuteAsync(
        AddItemToCartStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        //var shoppingCartId = step.CartId is null ? Guid.NewGuid() : Guid.Parse(step.CartId);

        //for now we will use participant id

        var cartItem = new ShoppingCartItem
        {
            ProductId = Guid.Parse(step.ItemId),
            Quantity = step.Quantity,
        };

        TempCart.AddItemToCart(context.ParticipantProcessId, cartItem);

        var cart = TempCart.GetCart(context.ParticipantProcessId);

        var response = new AddItemToCartResponse();

        return ProcessStepHandlerResult<AddItemToCartResponse>.Success(response);
    }
}
