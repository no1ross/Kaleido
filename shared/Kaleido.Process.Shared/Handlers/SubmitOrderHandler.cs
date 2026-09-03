//using Kaleido.Process.Processor.Execution;
//using Kaleido.Process.Shared.Responses;
//using Kaleido.Process.Shared.Steps;
//using Microsoft.EntityFrameworkCore;

//namespace Kaleido.Process.Shared.Handlers;

//public sealed class SubmitOrderHandler(
//    ShoppingCartDbContext dbContext)
//    : IProcessStepHandler<SubmitOrderStep, SubmitOrderResponse>
//{
//    public async Task<ProcessStepHandlerResult<SubmitOrderResponse>> ExecuteAsync(
//        SubmitOrderStep step,
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

//        var issues =
//            new List<ProcessIssue>();

//        if (order.BillingInfo is null)
//        {
//            issues.Add(
//                new ProcessIssue
//                {
//                    Code = "BillingMissing",
//                    Message = "Billing information has not been submitted.",
//                    Severity = Severity.Error
//                });
//        }
//        else if (!order.BillingInfo.Accepted || !order.BillingInfo.Validated)
//        {
//            issues.Add(
//                new ProcessIssue
//                {
//                    Code = "PaymentCorrectionRequired",
//                    Message = "Payment information must be corrected before the order can be submitted.",
//                    Severity = Severity.Error
//                });
//        }

//        if (!order.TermsAccepted)
//        {
//            issues.Add(
//                new ProcessIssue
//                {
//                    Code = "TermsNotAccepted",
//                    Message = "Terms and conditions must be accepted before the order can be submitted.",
//                    Severity = Severity.Error
//                });
//        }

//        var requiresPaymentCorrection =
//            issues.Any(x =>
//                string.Equals(
//                    x.Code,
//                    "PaymentCorrectionRequired",
//                    StringComparison.OrdinalIgnoreCase));

//        if (issues.Count == 0)
//        {
//            order.Submitted = true;
//            order.SubmissionId = $"sub-{Guid.NewGuid():N}";
//            order.SubmittedOn = now;
//            order.Status = OrderStatus.Submitted;
//            order.UpdatedOn = now;

//            await dbContext.SaveChangesAsync(cancellationToken);

//            var submittedResponse =
//                new SubmitOrderResponse
//                {
//                    SubmissionId = order.SubmissionId,
//                    Submitted = true,
//                    RequiresPaymentCorrection = false,
//                    Issues = Array.Empty<ProcessIssue>()
//                };

//            return new ProcessStepHandlerResult<SubmitOrderResponse>
//            {
//                Response = submittedResponse
//            };
//        }

//        order.UpdatedOn = now;

//        await dbContext.SaveChangesAsync(cancellationToken);

//        var response =
//            new SubmitOrderResponse
//            {
//                SubmissionId = order.SubmissionId ?? string.Empty,
//                Submitted = false,
//                RequiresPaymentCorrection = requiresPaymentCorrection,
//                Issues = issues
//            };

//        if (requiresPaymentCorrection)
//        {
//            return new ProcessStepHandlerResult<SubmitOrderResponse>
//            {
//                Response = response,
//                RequiredStep = "ChangePaymentInfo"
//            };
//        }

//        return new ProcessStepHandlerResult<SubmitOrderResponse>
//        {
//            Response = response
//        };
//    }
//}