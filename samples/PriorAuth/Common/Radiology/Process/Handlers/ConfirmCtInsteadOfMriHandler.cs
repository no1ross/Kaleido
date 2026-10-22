using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

public sealed class ConfirmCtInsteadOfMriHandler(
    ConfiguredInformationRequests informationRequests)
    : IProcessStepHandler<ConfirmCtInsteadOfMriStep>
{
    // The binding key the business configures on the CT confirmation questionnaire.
    private const string ConfirmCtItem = "ConfirmCt";

    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        ConfirmCtInsteadOfMriStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        await informationRequests.RecordAnswersAsync(context.ProcessId, nameof(ConfirmCtInsteadOfMriStep), processStep, cancellationToken);

        return bool.TryParse(ConfiguredInformationRequests.Answer(processStep, ConfirmCtItem), out var confirmed) && confirmed
            ? ProcessStepHandlerResult.Success()
            : ProcessStepHandlerResult.Failure(RadiologyProcessMessages.CtNotConfirmed());
    }
}
