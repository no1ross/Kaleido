namespace Kaleido.Samples.ECommerce;

public enum OrderStatus
{
    Started = 1,
    BillingSubmitted = 2,
    TermsAccepted = 3,
    Submitted = 4,
    Cancelled = 5
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