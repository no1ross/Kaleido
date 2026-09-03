//using Kaleido.Process.Processor.Execution;
//using Kaleido.Process.Shared.Data;
//using Kaleido.Process.Shared.Responses;
//using Kaleido.Process.Shared.Steps;
//using Microsoft.EntityFrameworkCore;

//namespace Kaleido.Process.Shared.Handlers;

//public sealed class SubmitBillingHandler(
//    ShoppingCartDbContext dbContext)
//    : IProcessStepHandler<SubmitBillingStep, SubmitBillingResponse>
//{
//    public async Task<ProcessStepHandlerResult<SubmitBillingResponse>> ExecuteAsync(
//        SubmitBillingStep step,
//        ProcessStepContext context,
//        CancellationToken cancellationToken = default)
//    {
//        var now =
//            DateTimeOffset.UtcNow;

//        var orderId =
//            Guid.Parse(step.OrderId);

//        var order =
//            await dbContext.Orders
//                .Include(x => x.BillingInfo)
//                .SingleAsync(
//                    x => x.OrderId == orderId,
//                    cancellationToken);

//        var accepted =
//            !step.PaymentToken.StartsWith(
//                "invalid",
//                StringComparison.OrdinalIgnoreCase);

//        var authorizedAmount =
//            accepted
//                ? (decimal?)await CalculateCartTotalAsync(
//                    order.ShoppingCartId,
//                    cancellationToken)
//                : null;

//        if (order.BillingInfo is null)
//        {
//            order.BillingInfo =
//                new BillingInfo
//                {
//                    BillingInfoId = Guid.NewGuid(),
//                    OrderId = order.OrderId,
//                    PaymentMethod = step.PaymentMethod,
//                    PaymentToken = step.PaymentToken,
//                    BillingAddress = step.BillingAddress,
//                    Accepted = accepted,
//                    Validated = accepted,
//                    AuthorizedAmount = authorizedAmount,
//                    CreatedOn = now,
//                    UpdatedOn = now
//                };
//        }
//        else
//        {
//            order.BillingInfo.PaymentMethod = step.PaymentMethod;
//            order.BillingInfo.PaymentToken = step.PaymentToken;
//            order.BillingInfo.BillingAddress = step.BillingAddress;
//            order.BillingInfo.Accepted = accepted;
//            order.BillingInfo.Validated = accepted;
//            order.BillingInfo.AuthorizedAmount = authorizedAmount;
//            order.BillingInfo.UpdatedOn = now;
//        }

//        order.Status = OrderStatus.PendingSubmission;
//        order.UpdatedOn = now;

//        await dbContext.SaveChangesAsync(cancellationToken);

//        var warnings =
//            accepted
//                ? Array.Empty<string>()
//                : new[] { "Payment token was not accepted." };

//        var response =
//            new SubmitBillingResponse
//            {
//                BillingId = order.BillingInfo.BillingInfoId.ToString(),
//                Accepted = accepted,
//                PaymentMethod = step.PaymentMethod,
//                AuthorizedAmount = authorizedAmount,
//                ValidationWarnings = warnings
//            };

//        return new ProcessStepHandlerResult<SubmitBillingResponse>
//        {
//            Response = response
//        };
//    }

//    private async Task<decimal> CalculateCartTotalAsync(
//        Guid shoppingCartId,
//        CancellationToken cancellationToken)
//    {
//        return await dbContext.ShoppingCartItems
//            .Where(x => x.ShoppingCartId == shoppingCartId)
//            .SumAsync(
//                x => x.Quantity * x.UnitPrice,
//                cancellationToken);
//    }
//}