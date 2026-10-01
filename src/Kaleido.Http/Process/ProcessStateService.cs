using Kaleido.Http.Authorization;
using Kaleido.Process.Context;
using Kaleido.Process.Registry;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Process;

internal interface IProcessStateService
{
    Task<ProcessStateResponse?> GetCurrentState(
        Guid processId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Transfers process ownership to the authenticated caller. Returns
    /// <c>null</c> when the process does not exist (404); throws
    /// <see cref="KaleidoAuthorizationException"/> when the caller may not
    /// take it (unowned processes and role-mates of the owner may).
    /// </summary>
    Task<ProcessorContext?> TransferOwnershipAsync(
        Guid processId,
        CancellationToken cancellationToken);
}

internal sealed class ProcessStateService(
    IProcessContextStore contextStore,
    IProcessStepRegistry registry,
    KaleidoServiceOptions serviceOptions,
    IProcessResponseFactory responseFactory,
    IKaleidoAuthorizer authorizer,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    ILogger<ProcessStateService> logger)
    : IProcessStateService
{
    public async Task<ProcessStateResponse?> GetCurrentState(Guid processId, CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Loading process state for processor {ProcessorName} process {ProcessId}.",
            serviceOptions.ServiceName,
            processId);

        var context = await contextStore.LoadAsync(processId, cancellationToken);

        if (context == null)
        {
            logger.LogDebug(
                "Process state not found for processor {ProcessorName} process {ProcessId}.",
                serviceOptions.ServiceName,
                processId);

            return null;
        }

        // Owned processes are visible only to the owner and role-mates.
        // Unowned processes pass through.
        authorizer.AuthorizeProcess(context);

        logger.LogDebug(
            "Process state loaded for processor {ProcessorName} process {ProcessId} state {State}.",
            serviceOptions.ServiceName,
            processId,
            context.State);

        return new ProcessStateResponse
        {
            ProcessId = context.ProcessId,

            State = context.State,

            // RequiredStep is null when TargetProcessorName is set —
            // consumer must call the target processor's state endpoint instead.
            RequiredStep =
                context.TargetProcessorName is null
                    ? context.RequiredStep
                    : null,

            TargetProcessorName =
                context.TargetProcessorName,

            AvailableSteps =
                context.AvailableSteps
                    .Select(stepName =>
                        {
                            var registration = registry.Find(stepName)
                                ?? throw new KaleidoFrameworkException(
                                    FrameworkErrorCodes.MissingRegistration,
                                    $"Available step '{stepName}' was not found in the local registry.");
                            return responseFactory.CreateStepSummary(
                                new ProcessorStepSummary
                                {
                                    Name = registration.Metadata.Name,
                                    Description = registration.Metadata.Description,
                                    DisplayName = registration.Metadata.DisplayName,
                                    Version = registration.Metadata.Version,
                                    Repeatable = registration.Repeatable.Enabled
                                },
                                serviceOptions.ServiceName);
                        })
                    .ToArray(),

            Steps =
                context.Steps
                    .OrderBy(x => x.StepName, StringComparer.OrdinalIgnoreCase)
                    .Select(x =>
                        new ProcessStepHistory
                        {
                            StepName = x.StepName,
                            Version = x.Version,
                            Status = x.Status,
                            LastExecuted = x.LastExecuted
                        })
                    .ToArray(),

            CreatedUtc = context.CreatedUtc,

            UpdatedUtc = context.UpdatedUtc,

            Owner = context.Owner,
        };
    }

    public async Task<ProcessorContext?> TransferOwnershipAsync(
        Guid processId,
        CancellationToken cancellationToken)
    {
        var context = await contextStore.LoadAsync(processId, cancellationToken);

        if (context is null)
        {
            return null;
        }

        var correlation = correlationAccessor.Current;

        if (correlation.CallerName is null)
        {
            throw new KaleidoAuthorizationException(
                $"process '{processId}'",
                callerIsAuthenticated: false);
        }

        // Owned processes transfer only to the owner or a role-mate;
        // unowned processes may be taken by any authenticated caller.
        if (context.Owner is not null)
        {
            authorizer.AuthorizeProcess(context);
        }

        var transferred =
            context with
            {
                Owner = correlation.CallerName,
                OwnerRoles = correlation.CallerRoles
            };

        await contextStore.SaveAsync(transferred, cancellationToken);

        logger.LogDebug(
            "Process {ProcessId} ownership transferred to {Owner}.",
            processId,
            correlation.CallerName);

        return transferred;
    }
}
