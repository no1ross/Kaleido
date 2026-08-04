namespace Kaleido.Process.Shared;

public enum OrderStatus
{
    Draft,
    PendingSubmission,
    Submitted,
    Cancelled
}

public enum ShoppingCartStatus
{
    Active,
    ConvertedToOrder,
    Abandoned
}

public enum PaymentMethodType
{
    CreditCard,
    DebitCard,
    HsaCard,
    BankAccount
}

public enum OrderPriority
{
    Standard,
    Expedited,
    Overnight
}

public enum CartItemType
{
    Product,
    Service,
    Subscription
}

public enum RefundMethod
{
    OriginalPayment,
    StoreCredit,
    GiftCard
}

public enum Severity
{
    Information,
    Warning,
    Error
}