using Kaleido.Http.Registry;
using Kaleido.Processor;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Processor;

// On the request that hands off, the runtime answers with the target's next steps by name (#198):
// they are resolved here against the target's published registry - its client's cached copy, the
// same read that checks a cross-service lookup (#524) - so each keeps the target's description,
// authorization and execute URL. A step the copy does not have, or a copy that cannot be read, is
// left out with the pro_not_permitted notice, and a Warning says which.
internal interface IHandOffStepResolver
{
    Task<ProcessContinuation> ResolveAsync(
        ProcessResult processResult,
        CancellationToken cancellationToken);
}

internal sealed partial class HandOffStepResolver(
    IKaleidoDownstreamRegistries downstream,
    ILogger<HandOffStepResolver> logger)
    : IHandOffStepResolver
{
    public async Task<ProcessContinuation> ResolveAsync(
        ProcessResult processResult,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processResult);

        // A later request on a process handed off earlier carries no answer: nothing to resolve.
        if (processResult.TargetProcessorName is not { } target ||
            (processResult.RequiredStep is null && processResult.AvailableSteps.Count == 0))
        {
            return new ProcessContinuation(RequiredStep: null, AvailableSteps: [], processResult.Notice);
        }

        if (await downstream.GetServiceAsync(target, cancellationToken) is not { } registry)
        {
            LogTargetRegistryUnread(logger, processResult.ProcessId, target);

            return new ProcessContinuation(RequiredStep: null, AvailableSteps: [], ProcessContinuationFilter.NotPermitted);
        }

        var published = registry.Processes
            .SelectMany(x => x.Steps ?? [])
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var missing = new List<string>();
        ProcessRequiredStep? required = null;

        if (processResult.RequiredStep is { } requiredName)
        {
            if (published.TryGetValue(requiredName, out var step))
            {
                required = Required(step, processResult.RequiredInformationRequest);
            }
            else
            {
                missing.Add(requiredName);
            }
        }

        var available = new List<ProcessStepSummary>();

        foreach (var name in processResult.AvailableSteps)
        {
            if (published.TryGetValue(name, out var step))
            {
                available.Add(Summary(step));
            }
            else
            {
                missing.Add(name);
            }
        }

        if (missing.Count > 0)
        {
            LogTargetStepsNotPublished(logger, processResult.ProcessId, target, string.Join(", ", missing));
        }

        return new ProcessContinuation(
            required,
            available,
            missing.Count > 0 ? ProcessContinuationFilter.NotPermitted : processResult.Notice);
    }

    private static ProcessStepSummary Summary(ProcessStepContract step) =>
        new()
        {
            Name = step.Name,
            Description = step.Description,
            DisplayName = step.DisplayName,
            Repeatable = step.Repeatable,
            IsInformationStep = step.IsInformationStep,
            Destructive = step.Destructive,
            External = step.External,
            RequiresConfirmation = step.RequiresConfirmation,
            Authorization = step.Authorization,
            ExecuteUrl = step.ExecuteUrl,
        };

    private static ProcessRequiredStep Required(ProcessStepContract step, InformationRequest? informationRequest) =>
        new()
        {
            Name = step.Name,
            Description = step.Description,
            DisplayName = step.DisplayName,
            Repeatable = step.Repeatable,
            IsInformationStep = step.IsInformationStep,
            Destructive = step.Destructive,
            External = step.External,
            RequiresConfirmation = step.RequiresConfirmation,
            Authorization = step.Authorization,
            ExecuteUrl = step.ExecuteUrl,
            InformationRequest = informationRequest,
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Process {ProcessId} was handed off to {Target}, whose registry could not be read through this host's client; its next steps are left out of the response (pro_not_permitted). Check the client for {Target}.")]
    private static partial void LogTargetRegistryUnread(ILogger logger, Guid processId, string target);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Process {ProcessId} was handed off to {Target}, whose registry, as this host's client reads it, does not publish steps {StepNames}; they are left out of the response (pro_not_permitted). Check that this service's credential may see them.")]
    private static partial void LogTargetStepsNotPublished(ILogger logger, Guid processId, string target, string stepNames);
}
