using System.ComponentModel.DataAnnotations;
using Kaleido.Client.Processor;

namespace Kaleido.Http.FunctionalTests.Processor.HandOff.Target
{
    // The "returns" service an order is handed off to (#198). Its first step answers with whatever
    // the test asks for next, so each kind of answer a target can give is exercised.
    public static class ReturnAnswers
    {
        public const string Questions = "questions";
        public const string Ordinary = "ordinary";
        public const string Choice = "choice";
        public const string Restricted = "restricted";
        public const string Nothing = "nothing";
        public const string Reject = "reject";

        public const string RefundRole = "refunds";
        public const string ReasonRequestId = "return-reason";
        public const string ReasonItemId = "return-reason-why";
        public const string InspectDisplayName = "Inspect the returned item";
        public const string ReturnNumber = "R-4711";

        public static readonly ProcessMessage Opened = new() { Code = "RETURN_OPENED", Type = MessageType.Information, Message = "The return is open." };
        public static readonly ProcessMessage Refused = new() { Code = "RETURN_REFUSED", Type = MessageType.Error, Message = "This order cannot be returned." };

        public static readonly InformationRequest Reason =
            new()
            {
                InformationRequestId = ReasonRequestId,
                Title = "Return reason",
                Items = [new InformationItem { Id = ReasonItemId, Text = "Why is the item returned?", Type = InformationItemType.Text }],
            };
    }

    [ProcessStep(DisplayName = "Open return", Description = "Opens the return and answers with what comes next.")]
    public sealed record OpenReturnStep : IProcessStep
    {
        [Required]
        public string Next { get; init; } = string.Empty;
    }

    // The target's own result: the origin reads it and puts what its caller needs into its own result.
    public sealed record OpenReturnResult
    {
        public string ReturnNumber { get; init; } = string.Empty;
    }

    [ProcessStep(DisplayName = ReturnAnswers.InspectDisplayName, Description = "The returns service inspects the item.")]
    [AvailableAfter<OpenReturnStep>]
    public sealed record InspectReturnStep : IProcessStep;

    [ProcessStep(DisplayName = "Refund", Description = "Refunds the returned order.")]
    [AvailableAfter<OpenReturnStep>]
    [KaleidoAuthorization(Roles = ReturnAnswers.RefundRole)]
    public sealed record RefundStep : IProcessStep;

    [ProcessStep(DisplayName = "Return reason", Description = "Answers why the item is returned.")]
    [AvailableAfter<OpenReturnStep>]
    public sealed record ReturnReasonStep : IInformationStep
    {
        public string InformationRequestId { get; init; } = string.Empty;
        public string ExchangeId { get; init; } = string.Empty;

        public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
    }

    public sealed class OpenReturnHandler : IProcessStepHandler<OpenReturnStep, OpenReturnResult>
    {
        private static readonly OpenReturnResult Opened = new() { ReturnNumber = ReturnAnswers.ReturnNumber };

        public Task<ProcessStepHandlerResult<OpenReturnResult>> ExecuteAsync(OpenReturnStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(processStep.Next switch
            {
                ReturnAnswers.Questions => ProcessStepHandlerResult.Success(Opened, ReturnAnswers.Opened).RequireInformation<ReturnReasonStep>(ReturnAnswers.Reason),
                ReturnAnswers.Ordinary => ProcessStepHandlerResult.Success(Opened, ReturnAnswers.Opened).Next<InspectReturnStep>(),
                ReturnAnswers.Restricted => ProcessStepHandlerResult.Success(Opened, ReturnAnswers.Opened).Next<RefundStep>(),
                ReturnAnswers.Nothing => ProcessStepHandlerResult.Complete(Opened, ReturnAnswers.Opened),
                ReturnAnswers.Reject => ProcessStepHandlerResult.Reject(ReturnAnswers.Refused),
                _ => ProcessStepHandlerResult.Success(Opened, ReturnAnswers.Opened),
            });
    }

    public sealed class InspectReturnHandler : IProcessStepHandler<InspectReturnStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(InspectReturnStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    public sealed class RefundHandler : IProcessStepHandler<RefundStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(RefundStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    public sealed class ReturnReasonHandler : IProcessStepHandler<ReturnReasonStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(ReturnReasonStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }
}

namespace Kaleido.Http.FunctionalTests.Processor.HandOff.Origin
{
    // The "orders" service: returning an order hands the process to the returns service, with that
    // service's answer and this service's own result built from the target's (#198); a return the
    // target refuses is refused here.
    [ProcessStep(DisplayName = "Place order", Description = "Places the order.")]
    public sealed record PlaceOrderStep : IProcessStep;

    [ProcessStep(DisplayName = "Return order", Description = "Hands the order to the returns service.")]
    [DependsOn<PlaceOrderStep>]
    [Repeatable]
    public sealed record ReturnOrderStep : IProcessStep
    {
        [Required]
        public string Next { get; init; } = string.Empty;
    }

    // The origin's own result: its caller's contract, filled from the target's result.
    public sealed record ReturnOrderResult
    {
        public string ReturnNumber { get; init; } = string.Empty;
    }

    public sealed class PlaceOrderHandler : IProcessStepHandler<PlaceOrderStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(PlaceOrderStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    public sealed class ReturnOrderHandler(IKaleidoProcessorClientFactory clients) : IProcessStepHandler<ReturnOrderStep, ReturnOrderResult>
    {
        public async Task<ProcessStepHandlerResult<ReturnOrderResult>> ExecuteAsync(ReturnOrderStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default)
        {
            var answer = await clients.GetClient(HandOffFixture.TargetService)
                .ExecuteStepAsync<Target.OpenReturnStep, Target.OpenReturnResult>(new Target.OpenReturnStep { Next = processStep.Next }, cancellationToken);

            return answer.Status == StepExecutionStatus.Completed
                ? ProcessStepHandlerResult.HandOff(HandOffFixture.TargetService, answer, new ReturnOrderResult { ReturnNumber = answer.Result?.ReturnNumber ?? string.Empty })
                : ProcessStepHandlerResult.Reject([.. answer.BusinessMessages]);
        }
    }
}
