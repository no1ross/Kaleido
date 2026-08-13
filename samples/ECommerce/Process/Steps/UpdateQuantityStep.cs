//using Kaleido.Process.Attributes;
//using System.ComponentModel.DataAnnotations;

//namespace Kaleido.Samples.ECommerce.Steps;

//[ProcessStep(
//    Name = "UpdateQuantity",
//    DisplayName = "Update Item Quantity",
//    Description = "Changes the quantity of an item in the shopping cart.",
//    Version = "1.0")]
//[DependsOnStep(typeof(AddItemToCartStep))]
//[Repeatable]
//public sealed record UpdateQuantityStep
//{
//    [Required]
//    public required string CartId { get; init; }

//    [Required]
//    public required string ItemId { get; init; }

//    [Range(1, 999)]
//    public required int Quantity { get; init; }
//}
