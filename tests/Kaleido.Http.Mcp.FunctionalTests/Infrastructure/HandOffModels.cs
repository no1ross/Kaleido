using System.Text.Json;
using Kaleido.Http.Processor;

namespace Kaleido.Http.Mcp.FunctionalTests.Infrastructure.HandOff
{
    // A clinic step that moves the visit to the "ward" service (#503): it admits the patient there
    // under the same process id, then hands off. Only HandOffFixture registers it.
    [ProcessStep(DisplayName = "Transfer to ward", Description = "Moves the patient to the ward.")]
    public sealed record TransferToWardStep : IProcessStep;

    public sealed class TransferToWardHandler(IKaleidoProcessorClientFactory clients)
        : IProcessStepHandler<TransferToWardStep>
    {
        public async Task<ProcessStepHandlerResult> ExecuteAsync(TransferToWardStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default)
        {
            await clients.GetClient(McpClinicFixture.WardService).ExecuteStepAsync(
                nameof(Ward.AdmitStep),
                JsonSerializer.SerializeToElement(new { }),
                context.ProcessId,
                idempotencyKey: null,
                cancellationToken);

            return ProcessStepHandlerResult.HandOff(McpClinicFixture.WardService);
        }
    }
}

namespace Kaleido.Http.Mcp.FunctionalTests.Infrastructure.HandOff.Ward
{
    // The "ward" service the clinic hands off to: admitted, then discharged.
    [ProcessStep(DisplayName = "Admit", Description = "Admits the patient to the ward.")]
    public sealed record AdmitStep : IProcessStep;

    [ProcessStep(DisplayName = "Discharge", Description = "Discharges the patient from the ward.")]
    [DependsOn<AdmitStep>]
    public sealed record DischargeStep : IProcessStep;

    public sealed class AdmitHandler : IProcessStepHandler<AdmitStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(AdmitStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    public sealed class DischargeHandler : IProcessStepHandler<DischargeStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(DischargeStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }
}
