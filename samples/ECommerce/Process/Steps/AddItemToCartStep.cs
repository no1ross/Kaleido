using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Processor;
using Kaleido.Samples.ECommerce.Steps;

namespace Kaleido.Samples.ECommerce.Process.Steps;

[ProcessStep(
    DisplayName = "Shopping Carts - Add Item to Cart",
    Description = "Adds one or more products to the shopping cart.",
    Version = "1.0")]
[AvailableUntil<SubmitOrderStep>]
[Repeatable]
public sealed record AddItemToCartStep : IProcessStep
{

    [Required]
    [StringLength(100)]
    [Description("The ID (a GUID) of the catalog product to add.")]
    public required string ItemId
    {
        get;
        init;
    }

    [Description("The customer who owns the cart, if known; used to find their active cart.")]
    public Guid? CustomerId
    {
        get;
        init;
    }

    [Range(1, 999)]
    [Description("How many units to add (1-999).")]
    public required int Quantity
    {
        get;
        init;
    }
}
