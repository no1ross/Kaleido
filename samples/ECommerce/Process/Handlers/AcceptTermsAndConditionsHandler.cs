//using Kaleido.Process.Participant.Execution;
//using Kaleido.Process.Shared.Responses;
//using Kaleido.Process.Shared.Steps;
//using Kaleido.Samples.ECommerce.Data;
//using Microsoft.EntityFrameworkCore;

//namespace Kaleido.Process.Shared.Handlers;

//public sealed class AcceptTermsAndConditionsHandler(
//    ECommerceDbContext dbContext)
//    : IProcessStepHandler<AcceptTermsAndConditionsStep, AcceptTermsAndConditionsResponse>
//{
//    public async Task<ProcessStepHandlerResult<AcceptTermsAndConditionsResponse>> ExecuteAsync(
//        AcceptTermsAndConditionsStep step,
//        ProcessStepContext context,
//        CancellationToken cancellationToken = default)
//    {
//        var orderId =
//            Guid.Parse(step.OrderId);

//        var order =
//            await dbContext.Orders
//                .SingleAsync(
//                    x => x.OrderId == orderId,
//                    cancellationToken);

//        order.TermsAccepted = step.Accepted;
//        order.TermsAcceptedOn = step.AcceptedOn;
//        order.UpdatedOn = DateTimeOffset.UtcNow;

//        await dbContext.SaveChangesAsync(cancellationToken);

//        var response =
//            new AcceptTermsAndConditionsResponse
//            {
//                Accepted = order.TermsAccepted,
//                TermsVersion = "1.0",
//                AcceptedOn = step.AcceptedOn
//            };

//        return new ProcessStepHandlerResult<AcceptTermsAndConditionsResponse>
//        {
//            Response = response
//        };
//    }
//}