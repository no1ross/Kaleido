//using Kaleido.Process.Attributes;
//using System.ComponentModel.DataAnnotations;

//namespace Kaleido.Samples.ECommerce.Steps;

//[ProcessStep(
//    Name = "CancelOrder",
//    DisplayName = "Cancel Order",
//    Description = "Cancels an order that has already been submitted.",
//    Version = "1.0")]
//[AvailableAfter(typeof(SubmitOrderStep))]
//public sealed record CancelOrderStep
//{
//    [Required]
//    public required string OrderId { get; init; }

//    [Required]
//    [StringLength(500)]
//    public required string CancellationReason { get; init; }

//    public bool RefundRequested { get; init; }
//}
