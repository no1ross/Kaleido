using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

public sealed class ConfirmCtInsteadOfMriHandler
    : IProcessStepHandler<ConfirmCtInsteadOfMriStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        ConfirmCtInsteadOfMriStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        var confirmed =
            processStep.Items
                .FirstOrDefault(x => x.ItemId == QuestionnaireDefinitionClient.CtConfirmationItemId)?
                .Answers
                .FirstOrDefault()?
                .Value;

        return Task.FromResult(
            bool.TryParse(confirmed, out var value) && value
                ? ProcessStepHandlerResult.Success()
                : ProcessStepHandlerResult.Failure(RadiologyProcessMessages.CtNotConfirmed()));
    }
}
