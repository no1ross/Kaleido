using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Process.Steps;

[ProcessStep(
    Name = "AddItemToCart",
    DisplayName = "Add Item to Cart",
    Description = "Adds one or more products to the shopping cart.",
    Version = "1.0")]
[Repeatable]
public sealed record AddItemToCartStep
{
    [Required]
    [StringLength(100)]
    public required string ItemId
    {
        get;
        init;
    }

    [Range(1, 999)]
    public required int Quantity
    {
        get;
        init;
    }
}
