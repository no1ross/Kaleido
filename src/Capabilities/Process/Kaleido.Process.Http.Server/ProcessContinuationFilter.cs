using Microsoft.Extensions.Logging;

namespace Kaleido.Process.Http.Server;

// How a process continues, as the caller may see it (#524): a caller is never described a step they
// may not run. The required step they may not run is left out, and so is every available step they
// may not run; when that leaves them nothing to continue with, the notice says so without naming
// the steps. A Warning names them for the operator: a caller who reached the process but may not
// continue it is a role misconfiguration more often than not.
internal interface IProcessContinuationFilter
{
    Task<ProcessContinuation> FilterAsync(
        Guid processId,
        string? requiredStep,
        IReadOnlyCollection<string> availableSteps,
        string? targetProcessorName,
        CancellationToken cancellationToken);
}

[ExcludeFromCodeCoverage]
internal sealed record ProcessContinuation(
    string? RequiredStep,
    IReadOnlyCollection<string> AvailableSteps,
    KaleidoNotice? Notice);

internal sealed partial class ProcessContinuationFilter(
    IProcessorStepRegistry registry,
    IKaleidoAuthorizer authorizer,
    ILogger<ProcessContinuationFilter> logger)
    : IProcessContinuationFilter
{
    internal static readonly KaleidoNotice NotPermitted =
        new(ProcessorErrorCodes.NotPermitted, "You do not have access to continue this process.");

    public async Task<ProcessContinuation> FilterAsync(
        Guid processId,
        string? requiredStep,
        IReadOnlyCollection<string> availableSteps,
        string? targetProcessorName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(availableSteps);
        if (!authorizer.IsEnforced || targetProcessorName is not null)
        {
            return new ProcessContinuation(requiredStep, availableSteps, Notice: null);
        }

        KaleidoNotice? notice = null;
        if (requiredStep is not null && !await authorizer.CanAccessAsync(AuthorizationOf(requiredStep), cancellationToken))
        {
            LogRequiredStepNotPermitted(logger, processId, requiredStep);
            requiredStep = null;
            notice = NotPermitted;
        }

        var permitted = await authorizer.FilterAsync(availableSteps, AuthorizationOf, cancellationToken);
        if (availableSteps.Count > 0 && permitted.Count == 0)
        {
            LogNoAvailableStepPermitted(logger, processId, string.Join(", ", availableSteps));
            notice = NotPermitted;
        }

        return new ProcessContinuation(requiredStep, permitted, notice);
    }

    private AuthorizationMetadata? AuthorizationOf(string name) =>
        registry.Find(name)?.Metadata.Authorization
        ?? throw new KaleidoFrameworkException(FrameworkErrorCodes.MissingRegistration,
            $"Next step '{name}' was not found in this service's Process catalog.");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Process {ProcessId} requires step {StepName}, which the caller may not run; it is left out of the response (pro_not_permitted). Check the caller's roles.")]
    private static partial void LogRequiredStepNotPermitted(ILogger logger, Guid processId, string stepName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Process {ProcessId} offers steps {StepNames}, none of which the caller may run; they are left out of the response (pro_not_permitted). Check the caller's roles.")]
    private static partial void LogNoAvailableStepPermitted(ILogger logger, Guid processId, string stepNames);
}
