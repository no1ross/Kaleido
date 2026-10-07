//using Kaleido.Processor;
//using System.ComponentModel.DataAnnotations;

//namespace Kaleido.Samples.ECommerce.Steps;

//[ProcessStep(
//    DisplayName = "Cancel Order",
//    Description = "Cancels an order that has already been submitted.",
//    Version = "1.0")]
//[AvailableAfter<SubmitOrderStep>]
//public sealed record CancelOrderStep : IProcessStep
//{
//    [Required]
//    public required string OrderId { get; init; }

//    [Required]
//    [StringLength(500)]
//    public required string CancellationReason { get; init; }

//    public bool RefundRequested { get; init; }
//}
