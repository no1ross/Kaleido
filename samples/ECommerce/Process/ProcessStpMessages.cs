using Kaleido.Process.Participant;

namespace Kaleido.Samples.ECommerce.Process;

public static class ProcessStpMessages
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

    public static ProcessMessage ShoppingCartCreated(
        Guid shoppingCartId
        ) =>
        new()
        {
            Code = "SHOPPING_CART_CREATED",
            Type = MessageType.Information,
            Message = $"A new shopping cart was created {shoppingCartId}."
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
        Guid processId) =>
        new()
        {
            Code = "SHOPPING_CART_PROCESS_MISMATCH",
            Type = MessageType.Error,
            Message =
                $"Shopping cart '{shoppingCartId}' is not associated with process '{processId}'."
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

    public static ProcessMessage CustomerNotFound(
        Guid customerId) =>
        new()
        {
            Code = "CUSTOMER_NOT_FOUND",
            Type = MessageType.Error,
            Message =
                $"Customer '{customerId}' was not found."
        };

    public static ProcessMessage CustomerInactive(
        Guid customerId) =>
        new()
        {
            Code = "CUSTOMER_INACTIVE",
            Type = MessageType.Error,
            Message =
                $"Customer '{customerId}' is inactive and cannot be selected."
        };

    public static ProcessMessage CustomerSelected(
        string firstName,
        string lastName) =>
        new()
        {
            Code = "CUSTOMER_SELECTED",
            Type = MessageType.Information,
            Message =
                $"Customer '{firstName} {lastName}' was successfully selected."
        };
    public static ProcessMessage AnonymousCartTransferredToCustomer(
        string firstName,
        string lastName) =>
        new()
        {
            Code = "ANONYMOUS_CART_TRANSFERRED",
            Type = MessageType.Information,
            Message =
                $"The anonymous cart was transferred to '{firstName} {lastName}'."
        };

    public static ProcessMessage AnonymousCartMergedIntoCustomerCart(
        string firstName,
        string lastName) =>
        new()
        {
            Code = "CUSTOMER_CART_MERGED",
            Type = MessageType.Information,
            Message =
                $"The anonymous cart was merged into '{firstName} {lastName}' existing cart."
        };

    public static ProcessMessage CustomerCartActivated(
        string firstName,
        string lastName) =>
        new()
        {
            Code = "CUSTOMER_CART_ACTIVATED",
            Type = MessageType.Information,
            Message =
                $"'{firstName} {lastName}' existing shopping cart was activated."
        };

    public static ProcessMessage CustomerCartMergedIntoCurrentCart(
        string firstName,
        string lastName) =>
        new()
        {
            Code = "CUSTOMER_CART_MERGED_INTO_CURRENT_CART",
            Type = MessageType.Information,
            Message =
                $"'{firstName} {lastName}' existing cart was merged into the current cart."
        };

    public static ProcessMessage CustomerRequiredForCheckout() =>
        new()
        {
            Code = "CUSTOMER_REQUIRED_FOR_CHECKOUT",
            Type = MessageType.Error,
            Message =
                "A customer must be selected before the shopping cart can be processed."
        };

    public static ProcessMessage ShoppingCartEmpty(
        Guid shoppingCartId) =>
        new()
        {
            Code = "SHOPPING_CART_EMPTY",
            Type = MessageType.Error,
            Message =
                $"Shopping cart '{shoppingCartId}' does not contain any items."
        };

    public static ProcessMessage ShoppingCartCustomerMismatch(
        Guid shoppingCartId,
        Guid customerId) =>
        new()
        {
            Code = "SHOPPING_CART_CUSTOMER_MISMATCH",
            Type = MessageType.Error,
            Message =
                $"Shopping cart '{shoppingCartId}' is not associated with customer '{customerId}'."
        };

    public static ProcessMessage OrderStarted(
        Guid orderId) =>
        new()
        {
            Code = "ORDER_STARTED",
            Type = MessageType.Information,
            Message =
                $"Order '{orderId}' was started from the shopping cart."
        };

    public static ProcessMessage OrderUpdatedFromCart(
        Guid orderId) =>
        new()
        {
            Code = "ORDER_UPDATED_FROM_CART",
            Type = MessageType.Information,
            Message =
                $"Order '{orderId}' was updated from the current shopping cart."
        };

    public static ProcessMessage OrderNotFound(
        Guid orderId) =>
        new()
        {
            Code = "ORDER_NOT_FOUND",
            Type = MessageType.Error,
            Message =
                $"Order '{orderId}' was not found."
        };

    public static ProcessMessage OrderCustomerMismatch(
        Guid orderId,
        Guid customerId) =>
        new()
        {
            Code = "ORDER_CUSTOMER_MISMATCH",
            Type = MessageType.Error,
            Message =
                $"Order '{orderId}' is not associated with customer '{customerId}'."
        };

    public static ProcessMessage OrderProcessMismatch(
        Guid orderId,
        Guid processId) =>
        new()
        {
            Code = "ORDER_PROCESS_MISMATCH",
            Type = MessageType.Error,
            Message =
                $"Order '{orderId}' is not associated with process '{processId}'."
        };

    public static ProcessMessage OrderNotStarted(
        Guid orderId,
        OrderStatus status) =>
        new()
        {
            Code = "ORDER_NOT_STARTED",
            Type = MessageType.Error,
            Message =
                $"Order '{orderId}' cannot be submitted because its current status is '{status}'."
        };

    public static ProcessMessage OrderContainsNoItems(
        Guid orderId) =>
        new()
        {
            Code = "ORDER_CONTAINS_NO_ITEMS",
            Type = MessageType.Error,
            Message =
                $"Order '{orderId}' cannot be submitted because it does not contain any items."
        };
    public static ProcessMessage OrderSubmitted(
        string orderNumber) =>
        new()
        {
            Code = "ORDER_SUBMITTED",
            Type = MessageType.Information,
            Message =
                $"Order '{orderNumber}' was submitted successfully."
        };


}