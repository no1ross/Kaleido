using System.Diagnostics.CodeAnalysis;
using Kaleido.Http.Authorization;
using Kaleido.Processor;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Processor;

// How a process continues, as the caller may see it (#524): a caller is never described a step they
// may not run. The required step they may not run is left out, and so is every available step they
// may not run; when that leaves them nothing to continue with, the notice says so without naming
// the steps. A Warning names them for the operator: a caller who reached the process but may not
// continue it is a role misconfiguration more often than not.
internal interface IProcessContinuationFilter
{
    Task<ProcessContinuation> FilterAsync(
        Guid processId,
        ProcessRequiredStep? requiredStep,
        IReadOnlyCollection<ProcessStepSummary> availableSteps,
        CancellationToken cancellationToken);
}

[ExcludeFromCodeCoverage]
internal sealed record ProcessContinuation(
    ProcessRequiredStep? RequiredStep,
    IReadOnlyCollection<ProcessStepSummary> AvailableSteps,
    KaleidoNotice? Notice);

internal sealed partial class ProcessContinuationFilter(
    IKaleidoAuthorizer authorizer,
    ILogger<ProcessContinuationFilter> logger)
    : IProcessContinuationFilter
{
    internal static readonly KaleidoNotice NotPermitted =
        new(ProcessorErrorCodes.NotPermitted, "You do not have access to continue this process.");

    public async Task<ProcessContinuation> FilterAsync(
        Guid processId,
        ProcessRequiredStep? requiredStep,
        IReadOnlyCollection<ProcessStepSummary> availableSteps,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(availableSteps);

        if (!authorizer.IsEnforced)
        {
            return new ProcessContinuation(requiredStep, availableSteps, Notice: null);
        }

        KaleidoNotice? notice = null;

        if (requiredStep is not null && !await authorizer.CanAccessAsync(requiredStep.Authorization, cancellationToken))
        {
            LogRequiredStepNotPermitted(logger, processId, requiredStep.Name);
            requiredStep = null;
            notice = NotPermitted;
        }

        var permitted = await authorizer.FilterAsync(availableSteps, x => x.Authorization, cancellationToken);

        if (availableSteps.Count > 0 && permitted.Count == 0)
        {
            LogNoAvailableStepPermitted(logger, processId, string.Join(", ", availableSteps.Select(x => x.Name)));
            notice = NotPermitted;
        }

        return new ProcessContinuation(requiredStep, permitted, notice);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Process {ProcessId} requires step {StepName}, which the caller may not run; it is left out of the response (pro_not_permitted). Check the caller's roles.")]
    private static partial void LogRequiredStepNotPermitted(ILogger logger, Guid processId, string stepName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Process {ProcessId} offers steps {StepNames}, none of which the caller may run; they are left out of the response (pro_not_permitted). Check the caller's roles.")]
    private static partial void LogNoAvailableStepPermitted(ILogger logger, Guid processId, string stepNames);
}
