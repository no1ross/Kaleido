//using Kaleido.Process.Attributes;
//using Kaleido.Samples.ECommerce;
//using Kaleido.Samples.ECommerce.Entities;
//using System.ComponentModel.DataAnnotations;

//namespace Kaleido.Samples.ECommerce.Steps;

//[ProcessStep(
//    Name = "StartOrder",
//    DisplayName = "Create Order",
//    Description = "Creates an order from the current shopping cart contents.",
//    Version = "1.0")]
//[DependsOnStep(typeof(AddItemToCartStep))]
//public sealed record StartOrderStep
//{
//    [Required]
//    public required string CartId { get; init; }

//    [Required]
//    public required string MemberId { get; init; }

//    [Required]
//    public required OrderPriority Priority { get; init; }

//    [Required]
//    public required Address ShippingAddress { get; init; }
//}
