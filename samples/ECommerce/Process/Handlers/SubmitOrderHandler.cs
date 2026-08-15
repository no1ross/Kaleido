using Kaleido.Process.Participant.Execution;

using Kaleido.Samples.ECommerce.Data;
using Kaleido.Samples.ECommerce.Data.Entities;

using Kaleido.Samples.ECommerce.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.ECommerce.Process.Handlers;

internal sealed class SubmitOrderHandler(
    ECommerceDbContext dbContext)
    : IProcessStepHandler<SubmitOrderStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        SubmitOrderStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        var order =
            await dbContext.Orders
                .Include(x => x.Items)
                .Include(x => x.StatusHistory)
                .Include(x => x.ShoppingCart)
                .FirstOrDefaultAsync(
                    x => x.OrderId ==
                         step.OrderId,
                    cancellationToken);

        if (order is null)
        {
            return ProcessStepHandlerResult.Failure(
                ProcessStpMessages.OrderNotFound(
                    step.OrderId));
        }

        if (order.ParticipantProcessId !=
            context.ParticipantProcessId)
        {
            return ProcessStepHandlerResult.Failure(
                ProcessStpMessages.OrderProcessMismatch(
                    order.OrderId,
                    context.ParticipantProcessId));
        }

        if (order.Status !=
            OrderStatus.Started)
        {
            return ProcessStepHandlerResult.Failure(
                ProcessStpMessages.OrderNotStarted(
                    order.OrderId,
                    order.Status));
        }

        if (order.Items.Count == 0)
        {
            return ProcessStepHandlerResult.Failure(
                ProcessStpMessages.OrderContainsNoItems(
                    order.OrderId));
        }

        var submittedUtc =
            DateTime.UtcNow;

        var previousStatus =
            order.Status;

        order.Status =
            OrderStatus.Submitted;

        order.SubmittedUtc =
            submittedUtc;

        if (string.IsNullOrWhiteSpace(
                order.OrderNumber))
        {
            order.OrderNumber =
                GenerateOrderNumber(
                    submittedUtc);
        }

        dbContext.OrderStatusHistories.Add(
            new OrderStatusHistory
            {
                OrderStatusHistoryId =
                    Guid.NewGuid(),

                OrderId =
                    order.OrderId,

                FromStatus =
                    previousStatus,

                ToStatus =
                    OrderStatus.Submitted,

                ChangedUtc =
                    submittedUtc,

                Reason =
                    "Order submitted."
            });

        if (order.ShoppingCart is not null)
        {
            order.ShoppingCart.IsActive =
                false;

            order.ShoppingCart.UpdatedUtc =
                submittedUtc;
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        var cart = dbContext.ShoppingCarts.FirstOrDefault(x => x.ShoppingCartId == order.ShoppingCartId);

        return ProcessStepHandlerResult.Success(
            ProcessStpMessages.OrderSubmitted(
                order.OrderNumber));
    }

    private static string GenerateOrderNumber(
        DateTime submittedUtc)
    {
        return
            $"ORD-{submittedUtc:yyyyMMddHHmmss}-{Guid.NewGuid():N}"
                [..28]
                .ToUpperInvariant();
    }
}