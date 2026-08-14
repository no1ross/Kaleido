using Kaleido.Process.Participant;

namespace Kaleido.Samples.ECommerce.Process;

public static class ShoppingCartMessages
{
    public static ProcessMessage ProductNotFound(
        string productId) =>
        new()
        {
            Code = "PRODUCT_NOT_FOUND",
            Type = MessageType.Error,
            Message = $"Product '{productId}' was not found."
        };

    public static ProcessMessage CustomerRequired() =>
        new()
        {
            Code = "CUSTOMER_REQUIRED",
            Type = MessageType.Error,
            Message = "CustomerId is required to create a shopping cart."
        };

    public static ProcessMessage ActiveCartProcessMismatch(
        Guid cartProcessId,
        Guid currentProcessId) =>
        new()
        {
            Code = "ACTIVE_CART_PROCESS_MISMATCH",
            Type = MessageType.Error,
            Message =
                $"The active cart belongs to process '{cartProcessId}' but the current request is using process '{currentProcessId}'."
        };

    public static ProcessMessage ShoppingCartCreated() =>
        new()
        {
            Code = "SHOPPING_CART_CREATED",
            Type = MessageType.Information,
            Message = "A new shopping cart was created."
        };

    public static ProcessMessage ItemAddedToCart(
        string productName,
        int quantity) =>
        new()
        {
            Code = "ITEM_ADDED_TO_CART",
            Type = MessageType.Information,
            Message =
                $"Successfully added {quantity} of '{productName}' to your cart."
        };

    public static ProcessMessage ShoppingCartNotFound(
        Guid shoppingCartId) =>
        new()
        {
            Code = "SHOPPING_CART_NOT_FOUND",
            Type = MessageType.Error,
            Message =
                $"Shopping cart '{shoppingCartId}' was not found."
        };

    public static ProcessMessage ShoppingCartProcessMismatch(
        Guid shoppingCartId,
        Guid participantProcessId) =>
        new()
        {
            Code = "SHOPPING_CART_PROCESS_MISMATCH",
            Type = MessageType.Error,
            Message =
                $"Shopping cart '{shoppingCartId}' is not associated with process '{participantProcessId}'."
        };

    public static ProcessMessage ShoppingCartItemNotFound(
        Guid shoppingCartItemId) =>
        new()
        {
            Code = "SHOPPING_CART_ITEM_NOT_FOUND",
            Type = MessageType.Error,
            Message =
                $"Shopping cart item '{shoppingCartItemId}' was not found."
        };

    public static ProcessMessage ItemRemovedFromCart(
        string productName) =>
        new()
        {
            Code = "ITEM_REMOVED_FROM_CART",
            Type = MessageType.Information,
            Message =
                $"'{productName}' was removed from the shopping cart."
        };

    public static ProcessMessage ItemQuantityUpdated(
        string productName,
        int previousQuantity,
        int quantity) =>
        new()
        {
            Code = "ITEM_QUANTITY_UPDATED",
            Type = MessageType.Information,
            Message =
                $"Updated '{productName}' quantity from {previousQuantity} to {quantity}."
        };

}