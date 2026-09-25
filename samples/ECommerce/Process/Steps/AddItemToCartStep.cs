using System.ComponentModel.DataAnnotations;
using Kaleido.Process;
using Kaleido.Samples.ECommerce.Steps;

namespace Kaleido.Samples.ECommerce.Process.Steps;

[ProcessStep(
    Name = "add-item-to-cart",
    DisplayName = "Shopping Carts - Add Item to Cart",
    Description = "Adds one or more products to the shopping cart.",
    Version = "1.0")]
[AvailableUntil(typeof(SubmitOrderStep))]
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

    public Guid? CustomerId
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
