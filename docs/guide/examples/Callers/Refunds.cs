using Kaleido.Guide.Examples.Processes;
using Kaleido.Processor;
using Kaleido.Processor.Execution;

namespace Kaleido.Guide.Examples.Callers;

// snippet: callers-rule
// Only managers may refund. The other capabilities have no rule: under the default mode
// (Authenticated) any signed-in caller may use them.
[ProcessStep(
    Version = "1.0.0",
    DisplayName = "Refund order",
    Description = "Refunds the shipped order.",
    Destructive = true,
    External = true)]
[AvailableAfter<ShipOrderStep>]
[KaleidoAuthorization(Roles = "manager")]
public sealed record RefundOrderStep : IProcessStep;
// end snippet

public sealed class RefundOrderHandler : IProcessStepHandler<RefundOrderStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        RefundOrderStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}
