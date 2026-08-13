using Kaleido.Samples.ECommerce.Data.Entities;

namespace Kaleido.Samples.ECommerce.Data;

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