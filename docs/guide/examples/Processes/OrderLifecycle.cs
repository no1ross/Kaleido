using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Guide.Examples.Events;
using Kaleido.Guide.Examples.GettingStarted;
using Kaleido.Http.Processor;
using Kaleido.Processor;
using Kaleido.Processor.Execution;

namespace Kaleido.Guide.Examples.Processes;

// snippet: processes-relationships
// A note can be added any time after the order is placed, as often as needed.
[ProcessStep(
    DisplayName = "Add note",
    Description = "Adds a note to the order.")]
[AvailableAfter<PlaceOrderStep>]
[Repeatable]
public sealed record AddNoteStep : IProcessStep
{
    [Required]
    [Description("The note.")]
    public required string Text { get; init; }
}

// The order can be cancelled once it is placed, until it ships.
[ProcessStep(
    DisplayName = "Cancel order",
    Description = "Cancels the order before it ships.",
    Destructive = true,
    RequiresConfirmation = true)]
[AvailableAfter<PlaceOrderStep>]
[AvailableUntil<ShipOrderStep>]
public sealed record CancelOrderStep : IProcessStep;

// Shipping needs the confirmation first, and is no longer possible once the order is cancelled.
[ProcessStep(
    DisplayName = "Ship order",
    Description = "Hands the confirmed order to a carrier.",
    External = true)]
[DependsOn<ConfirmOrderStep>]
[AvailableUntil<CancelOrderStep>]
public sealed record ShipOrderStep : IProcessStep
{
    [Required]
    [Description("The carrier: post or courier.")]
    public required string Carrier { get; init; }

    [Required]
    [Pii]
    [Description("The delivery address.")]
    public required string Address { get; init; }
}
// end snippet

// snippet: processes-outcomes
public sealed class ShipOrderHandler : IProcessStepHandler<ShipOrderStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        ShipOrderStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        // Cannot proceed: the process ends.
        if (processStep.Address.Contains("Antarctica", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(ProcessStepHandlerResult.Failure(new ProcessMessage
            {
                Code = "no_delivery",
                Type = MessageType.Error,
                Message = "We do not deliver to Antarctica.",
            }));
        }

        // The caller can correct this or try again later: the step is not completed and the process
        // is left exactly as it was.
        if (processStep.Carrier == "courier")
        {
            return Task.FromResult(ProcessStepHandlerResult.Reject(new ProcessMessage
            {
                Code = "carrier_closed",
                Type = MessageType.Warning,
                Message = "The courier is not collecting today. Choose the post, or try again tomorrow.",
            }));
        }

        return Task.FromResult(ProcessStepHandlerResult.Success());
    }
}
// end snippet

// snippet: processes-handoff
// Returns are another service's business: the process continues there.
[ProcessStep(
    DisplayName = "Return order",
    Description = "Starts a return of the shipped order; the returns service continues the process.")]
[AvailableAfter<ShipOrderStep>]
public sealed record ReturnOrderStep : IProcessStep;

// HandOff only tells the caller where the process continues; the handler starts it there. Called
// from this step, the returns service starts its process under this process's id (a handoff).
public sealed class ReturnOrderHandler(IKaleidoProcessorClientFactory processors) : IProcessStepHandler<ReturnOrderStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        ReturnOrderStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        await processors.GetClient("returns").ExecuteStepAsync(new OpenReturnStep(), cancellationToken);

        return ProcessStepHandlerResult.HandOff("returns");
    }
}

// The returns service's step, mirrored by its name: not an IProcessStep here, it is not this service's.
public sealed record OpenReturnStep;
// end snippet

public sealed class AddNoteHandler : IProcessStepHandler<AddNoteStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        AddNoteStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}

public sealed class CancelOrderHandler : IProcessStepHandler<CancelOrderStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        CancelOrderStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}
