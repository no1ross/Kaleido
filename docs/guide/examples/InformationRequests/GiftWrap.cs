using System.ComponentModel;
using Kaleido.Guide.Examples.GettingStarted;
using Kaleido.Processor;
using Kaleido.Processor.Execution;

namespace Kaleido.Guide.Examples.InformationRequests;

// snippet: information-ask
// The questions are decided at runtime (here fixed, in a real service from your rules or
// configuration): the handler returns them instead of a result.
[ProcessStep(
    Version = "1.0.0",
    DisplayName = "Gift wrap",
    Description = "Asks whether and how the order should be gift wrapped.")]
[AvailableAfter<PlaceOrderStep>]
public sealed record GiftWrapStep : IProcessStep;

public sealed class GiftWrapHandler : IProcessStepHandler<GiftWrapStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        GiftWrapStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ProcessStepHandlerResult.RequireInformation<GiftWrapAnswersStep>(new InformationRequest
        {
            InformationRequestId = "gift-wrap",
            Title = "Gift wrapping",
            Items =
            [
                new InformationItem
                {
                    Id = "wrap",
                    Text = "Should the order be gift wrapped?",
                    Type = InformationItemType.Boolean,
                },
                // Asked only when the first answer is yes.
                new InformationItem
                {
                    Id = "paper",
                    Text = "Which wrapping paper?",
                    Type = InformationItemType.Choice,
                    Options =
                    [
                        new InformationOption { Value = "red", Display = "Red" },
                        new InformationOption { Value = "gold", Display = "Gold" },
                    ],
                    EnableWhen = [new InformationCondition { Question = "wrap", Operator = InformationConditionOperator.Equals, Answer = "true" }],
                },
            ],
        }));
}
// end snippet

// snippet: information-answer
// The information step: its payload is the answers, nothing else.
[ProcessStep(
    Version = "1.0.0",
    DisplayName = "Gift wrap answers",
    Description = "The answers to the gift-wrapping questions.")]
public sealed record GiftWrapAnswersStep : IInformationStep
{
    public required string InformationRequestId { get; init; }

    public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
}

public sealed record GiftWrapChoice
{
    [Description("The wrapping paper chosen; empty when the order is not wrapped.")]
    public string? Paper { get; init; }
}

public sealed class GiftWrapAnswersHandler : IProcessStepHandler<GiftWrapAnswersStep, GiftWrapChoice>
{
    public Task<ProcessStepHandlerResult<GiftWrapChoice>> ExecuteAsync(
        GiftWrapAnswersStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        // Kaleido checked the answers against the request it sent; what they mean is yours.
        var paper = processStep.Items.SingleOrDefault(x => x.ItemId == "paper")?.Answers is [var answer, ..] ? answer.Value : null;

        return Task.FromResult(ProcessStepHandlerResult.Success(new GiftWrapChoice { Paper = paper }));
    }
}
// end snippet
