using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Shared.Data;
using Kaleido.Process.Shared.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Process.Shared.Handlers;

public sealed class CancelOrderHandler(
    ShoppingCartDbContext dbContext)
    : IProcessStepHandler<CancelOrderStep, CancelOrderResponse>
{
    public async Task<ProcessStepHandlerResult<CancelOrderResponse>> ExecuteAsync(
        CancelOrderStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        var now =
            DateTimeOffset.UtcNow;

        var orderId =
            Guid.Parse(step.OrderId);

        var order =
            await dbContext.Orders
                .Include(x => x.Cancellation)
                .Include(x => x.BillingInfo)
                .SingleAsync(
                    x => x.OrderId == orderId,
                    cancellationToken);

        var cancellationNumber =
            order.Cancellation?.CancellationNumber
            ?? $"can-{Guid.NewGuid():N}";

        if (order.Cancellation is null)
        {
            order.Cancellation =
                new OrderCancellation
                {
                    OrderCancellationId = Guid.NewGuid(),
                    OrderId = order.OrderId,
                    CancellationNumber = cancellationNumber,
                    CancellationReason = step.CancellationReason,
                    RefundRequested = step.RefundRequested,
                    CancelledOn = now
                };
        }
        else
        {
            order.Cancellation.CancellationReason = step.CancellationReason;
            order.Cancellation.RefundRequested = step.RefundRequested;
            order.Cancellation.CancelledOn = now;
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedOn = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        var refund =
            step.RefundRequested
                ? new RefundInformation
                {
                    Amount = order.BillingInfo?.AuthorizedAmount ?? 0m,
                    ProcessedOn = now,
                    RefundMethod = RefundMethod.OriginalPayment
                }
                : null;

        var response =
            new CancelOrderResponse
            {
                CancellationNumber = cancellationNumber,
                Cancelled = true,
                Refund = refund
            };

        return new ProcessStepHandlerResult<CancelOrderResponse>
        {
            Response = response
        };
    }
}