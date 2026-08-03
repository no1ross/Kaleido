//using Kaleido.Process.Attributes;
//using System.ComponentModel.DataAnnotations;
//using System.Net;

//namespace Kaleido.Process.Shared;

//[ProcessStep(
//    "Validate Order",
//    "Validates the incoming order before processing.",
//    "1.0.0")]
//public sealed record ValidateOrderStepRequest
//{
//    [Required]
//    public Guid OrderId { get; init; }

//    [Required]
//    public Customer Customer { get; init; } = null!;

//    [Required]
//    [MinLength(1)]
//    public List<OrderItem> Items { get; init; } = [];

//    [Required]
//    public Address ShippingAddress { get; init; } = null!;

//    public Address? BillingAddress { get; init; }

//    [StringLength(25)]
//    public string? CouponCode { get; init; }

//    public CustomerType CustomerType { get; init; }

//    public Dictionary<string, string> Metadata { get; init; } = [];
//}

//[ProcessStep(
//    "Authorize Payment",
//    "Authorizes payment for the order.",
//    "1.0.0")]
//[DependsOnStep(typeof(ValidateOrderStep))]
//public sealed class AuthorizePaymentStepRequest
//{
//    [Required]
//    public Guid OrderId { get; set; }

//    [Range(0.01, 999999.99)]
//    public decimal Amount { get; set; }

//    [Required]
//    public PaymentMethod PaymentMethod { get; set; }

//    public PaymentCard? Card { get; set; }

//    [StringLength(30)]
//    public string? PurchaseOrderNumber { get; set; }

//    public bool CaptureImmediately { get; set; }
//}

//[ProcessStep(
//    "Reserve Inventory",
//    "Reserves inventory required to fulfill the order.",
//    "1.0.0")]
//[DependsOnStep(typeof(AuthorizePaymentStep))]
//public sealed class ReserveInventoryStepRequest
//{
//    [Required]
//    public Guid OrderId { get; set; }

//    [Required]
//    [MinLength(1)]
//    public List<InventoryReservationItem> Items { get; set; } = [];

//    public bool AllowBackOrder { get; set; }

//    public DateTimeOffset? ReservationExpiration { get; set; }
//}

//[ProcessStep(
//    "Back Order Review",
//    "Reviews inventory shortages and determines resolution.",
//    "1.0.0")]
//[DependsOnStep(typeof(ReserveInventoryStep))]
//public sealed class BackOrderReviewStepRequest
//{
//    [Required]
//    public Guid OrderId { get; set; }

//    [Required]
//    [MinLength(1)]
//    public List<string> BackOrderedSkus { get; set; } = [];

//    public BackOrderResolutionStrategy? Strategy { get; set; }

//    public DateTimeOffset? ExpectedInventoryDate { get; set; }

//    [StringLength(500)]
//    public string? Notes { get; set; }
//}

//[ProcessStep(
//    "Calculate Shipping",
//    "Calculates available shipping options and costs.",
//    "1.0.0")]
//[DependsOnStep(typeof(ReserveInventoryStep))]
//public sealed record CalculateShippingStepRequest
//{
//    [Required]
//    public Guid OrderId { get; init; }

//    [Required]
//    public Address Destination { get; init; } = null!;

//    [Required]
//    [MinLength(1)]
//    public List<ShipmentPackage> Packages { get; init; } = [];

//    public ShippingMethod PreferredMethod { get; init; }

//    public bool SignatureRequired { get; init; }
//}

//[ProcessStep(
//    "Generate Shipment",
//    "Creates shipment information for the order.",
//    "1.0.0")]
//[DependsOnStep(typeof(CalculateShippingStep))]
//public sealed class GenerateShipmentStepRequest
//{
//    [Required]
//    public Guid OrderId { get; set; }

//    [Required]
//    public Address Destination { get; set; } = null!;

//    [Required]
//    public ShippingMethod ShippingMethod { get; set; }

//    [Range(0.0, 10000.0)]
//    public decimal ShippingCost { get; set; }

//    public Dictionary<string, string> CarrierMetadata { get; set; } = [];
//}

//[ProcessStep(
//    "Complete Order",
//    "Marks the order as completed.",
//    "1.0.0")]
//[DependsOnStep(typeof(GenerateShipmentStep))]
//public sealed class CompleteOrderStepRequest
//{
//    [Required]
//    public Guid OrderId { get; set; }

//    [Required]
//    public Guid ShipmentId { get; set; }

//    [Required]
//    [StringLength(100)]
//    public string TrackingNumber { get; set; } = string.Empty;

//    public DateTimeOffset? DeliveredOn { get; set; }

//    public Dictionary<string, string> Metadata { get; set; } = [];
//}