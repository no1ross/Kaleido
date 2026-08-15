using System.Text.Json.Serialization;

namespace Kaleido.Samples.ECommerce;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderStatus
{
    Unknown,
    Started,
    BillingSubmitted,
    TermsAccepted,
    Submitted,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShoppingCartStatus
{
    Active,
    ConvertedToOrder,
    Abandoned
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentMethodType
{
    CreditCard,
    DebitCard,
    HsaCard,
    BankAccount
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderPriority
{
    Standard,
    Expedited,
    Overnight
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CartItemType
{
    Product,
    Service,
    Subscription
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RefundMethod
{
    OriginalPayment,
    StoreCredit,
    GiftCard
}
