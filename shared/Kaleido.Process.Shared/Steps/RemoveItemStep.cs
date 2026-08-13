//using Kaleido.Process.Attributes;
//using System.ComponentModel.DataAnnotations;

//namespace Kaleido.Samples.ECommerce.Steps;

//[ProcessStep(
//    Name = "RemoveItem",
//    DisplayName = "Remove Item from Cart",
//    Description = "Removes an existing item from the shopping cart.",
//    Version = "1.0")]
//[DependsOnStep(typeof(AddItemToCartStep))]
//[Repeatable]
//public sealed record RemoveItemStep
//{
//    [Required]
//    public required string CartId { get; init; }

//    [Required]
//    public required string ItemId { get; init; }
//}


