using Kaleido.Process.Execution;
using Kaleido.Samples.ECommerce.Data;
using Kaleido.Samples.ECommerce.Data.Entities;
using Kaleido.Samples.ECommerce.Responses;
using Kaleido.Samples.ECommerce.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Process.Shared.Handlers;

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

        //for now we will use processor id

        var cartItem = new ShoppingCartItem
        {
            ProductId = Guid.Parse(step.ItemId),
            Quantity = step.Quantity,
        };

        TempCart.AddItemToCart(context.ProcessId, cartItem);

        var cart = TempCart.GetCart(context.ProcessId);

        var response = new AddItemToCartResponse();

        return ProcessStepHandlerResult<AddItemToCartResponse>.Success(response);
    }
}

public static class TempCart
{
    private static readonly Dictionary<Guid, ShoppingCart> _carts = new();

    public static ShoppingCart GetOrCreateCart(Guid cartId)
    {
        if (!_carts.TryGetValue(cartId, out var cart))
        {
            cart = new ShoppingCart
            {
                ShoppingCartId = cartId,
                CustomerId = Guid.NewGuid(),
                IsActive = true,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            _carts[cartId] = cart;
        }
        return cart;
    }

    public static void UpdateCart(ShoppingCart cart)
    {
        cart.UpdatedUtc = DateTime.UtcNow;
        _carts[cart.ShoppingCartId] = cart;
    }

    public static void ClearCart(Guid cartId)
    {
        _carts.Remove(cartId);
    }

    public static void ClearAllCarts()
    {
        _carts.Clear();
    }

    public static IReadOnlyCollection<ShoppingCart> GetAllCarts()
    {
        return _carts.Values.ToList();
    }

    public static void RemoveCart(Guid cartId)
    {
        _carts.Remove(cartId);
    }

    public static bool CartExists(Guid cartId)
    {
        return _carts.ContainsKey(cartId);
    }

    public static ShoppingCart? GetCart(Guid cartId)
    {
        _carts.TryGetValue(cartId, out var cart);
        return cart;
    }

    public static void AddItemToCart(Guid cartId, ShoppingCartItem item)
    {
        var cart = GetOrCreateCart(cartId);
        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == item.ProductId);
        if (existingItem != null)
        {
            existingItem.Quantity += item.Quantity;
        }
        else
        {
            cart.Items.Add(item);
        }
        UpdateCart(cart);
    }

    public static void RemoveItemFromCart(Guid cartId, Guid productId)
    {
        var cart = GetCart(cartId);
        if (cart != null)
        {
            var itemToRemove = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (itemToRemove != null)
            {
                cart.Items.Remove(itemToRemove);
                UpdateCart(cart);
            }
        }
    }
}