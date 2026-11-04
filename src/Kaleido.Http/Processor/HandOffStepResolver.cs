using Kaleido.Http.Registry;
using Kaleido.Processor;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Processor;

// On the request that hands off, the runtime answers with the target's next steps by name (#198):
// they are resolved here against the target's processor in the full registry (the registry source's,
// the same one a cross-service lookup is checked against, #524) - its current version when the registry
// knows versions - so each keeps the target's description, authorization and execute URL. A step the
// registry does not have, or a target it does not have, is left out with the pro_not_permitted notice,
// and a Warning says which.
internal interface IHandOffStepResolver
{
    Task<ProcessContinuation> ResolveAsync(
        ProcessResult processResult,
        CancellationToken cancellationToken);
}

internal sealed partial class HandOffStepResolver(
    IKaleidoRegistrySource source,
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

        var registry = await source.GetAsync(maxParallelism: null, cancellationToken);

        if (PublishedSteps(registry, await source.GetAvailabilityAsync(cancellationToken), target) is not { } published)
        {
            LogTargetRegistryUnread(logger, processResult.ProcessId, target);

            return new ProcessContinuation(RequiredStep: null, AvailableSteps: [], ProcessContinuationFilter.NotPermitted);
        }

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

    // The target's published steps by name - of its current version when the registry knows the
    // target's versions; null when the registry has no processor for the target.
    private static Dictionary<string, ProcessStepResponse>? PublishedSteps(KaleidoRegistry registry, KaleidoRegistryAvailability availability, string target)
    {
        var current = availability.Services
            .FirstOrDefault(x => x.IsCurrent && string.Equals(x.ServiceName, target, StringComparison.OrdinalIgnoreCase))?
            .ServiceVersion;
        var processors = registry.Processes
            .Where(x => string.Equals(x.ServiceName, target, StringComparison.OrdinalIgnoreCase)
                && (current is null || string.Equals(x.ServiceVersion, current, StringComparison.Ordinal)))
            .ToList();

        return processors.Count == 0
            ? null
            : processors
                .SelectMany(x => x.Steps ?? [])
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Process {ProcessId} was handed off to {Target}, which this host's registry does not have (not registered, or its registry could not be read); its next steps are left out of the response (pro_not_permitted). Check that {Target} is registered, or the client for {Target}.")]
    private static partial void LogTargetRegistryUnread(ILogger logger, Guid processId, string target);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Process {ProcessId} was handed off to {Target}, whose registry, as this host reads it, does not publish steps {StepNames}; they are left out of the response (pro_not_permitted). Check that this service's credential may see them.")]
    private static partial void LogTargetStepsNotPublished(ILogger logger, Guid processId, string target, string stepNames);
}
