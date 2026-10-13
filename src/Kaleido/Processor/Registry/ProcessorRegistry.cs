using Kaleido.Registry;
using Microsoft.Extensions.Logging;

namespace Kaleido.Processor.Registry;

[ExcludeFromCodeCoverage]
public sealed record ProcessorRegistryItem
{
    /// <summary>
    /// Marks this processor as the entry point for the application workflow.
    /// When true, consumers should start with this processor.
    /// Only one processor in a distributed system should have this set to true.
    /// </summary>
    public bool IsEntryProcessor { get; init; }

    public IReadOnlyCollection<ProcessorStepSummary> InitialSteps { get; init; }
        = [];

    public IReadOnlyCollection<ProcessorStepRegistryItem> Steps { get; init; }
        = [];
}

[ExcludeFromCodeCoverage]
public sealed record ProcessorStepRegistryItem
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public bool Repeatable { get; init; }

    /// <summary>
    /// <see langword="true"/> when the step's input is the answers to an information request;
    /// <see cref="Fields"/> is then empty and the pending request describes what to answer.
    /// </summary>
    public bool IsInformationStep { get; init; }

    /// <inheritdoc cref="ProcessStepAttribute.Destructive"/>
    public bool Destructive { get; init; }

    /// <inheritdoc cref="ProcessStepAttribute.External"/>
    public bool External { get; init; }

    /// <inheritdoc cref="ProcessStepAttribute.RequiresConfirmation"/>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Effective authorization rule. <see cref="AuthorizationMetadata.Unspecified"/>
    /// means neither the capability nor its service declared a rule.
    /// </summary>
    public AuthorizationMetadata Authorization { get; init; } = AuthorizationMetadata.Unspecified;

    public IReadOnlyCollection<ProcessorInputFieldDescriptor> Fields { get; init; }
        = [];

    public IReadOnlyCollection<ProcessorStepSummary> Dependencies { get; init; }
        = [];

    public IReadOnlyCollection<ProcessorStepSummary> AvailableAfter { get; init; }
        = [];

    public IReadOnlyCollection<ProcessorStepSummary> AvailableUntil { get; init; }
        = [];

    public ProcessorStepResultDescriptor? Result { get; init; }
}

[ExcludeFromCodeCoverage]
public sealed record ProcessorStepSummary
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public bool Repeatable { get; init; }

    /// <summary>
    /// <see langword="true"/> when the step's input is the answers to an information request.
    /// </summary>
    public bool IsInformationStep { get; init; }

    /// <inheritdoc cref="ProcessStepAttribute.Destructive"/>
    public bool Destructive { get; init; }

    /// <inheritdoc cref="ProcessStepAttribute.External"/>
    public bool External { get; init; }

    /// <inheritdoc cref="ProcessStepAttribute.RequiresConfirmation"/>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Effective authorization rule. <see cref="AuthorizationMetadata.Unspecified"/>
    /// means neither the capability nor its service declared a rule.
    /// </summary>
    public AuthorizationMetadata Authorization { get; init; } = AuthorizationMetadata.Unspecified;
}

[ExcludeFromCodeCoverage]
public record ProcessorPropertyDescriptor
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required DataTypeDescriptor DataType { get; init; }

    /// <summary>
    /// The field's data classifications (<c>DataClassificationAttribute</c>, e.g. <c>"PriorAuth:PHI"</c>),
    /// so clients know which values are sensitive. Empty when the field is not classified.
    /// </summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];
}

[ExcludeFromCodeCoverage]
public record ProcessorInputFieldDescriptor : ProcessorPropertyDescriptor
{
    /// <summary>
    /// A short label for the input, from <c>[Display(Name = …)]</c>. A hint for UIs and AI
    /// agents, which may phrase the question their own way.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// A suggested prompt, from <c>[Display(Prompt = …)]</c>. A hint, like <see cref="DisplayName"/>.
    /// </summary>
    public string? Prompt { get; init; }

    /// <summary>
    /// Where the input's value comes from, from <c>[Lookup]</c>: a field of one result of a query.
    /// Null when the user supplies the value directly.
    /// </summary>
    public LookupMetadata? Lookup { get; init; }

    public IReadOnlyCollection<ConstraintContract> Constraints { get; init; }
        = [];
}

/// <summary>
/// A step input's lookup, as published in the registry: the input takes <see cref="Field"/> of
/// one result of the query <see cref="Service"/>/<see cref="Source"/>[/<see cref="View"/>];
/// the inputs of a step with the same <see cref="Group"/> are filled from the same result.
/// </summary>
/// <remarks>
/// A published link, never a choice: the user identifies and picks the result (ADR 0008).
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record LookupMetadata
{
    /// <summary>The service that publishes the query.</summary>
    public required string Service { get; init; }

    /// <summary>The query source's published name.</summary>
    public required string Source { get; init; }

    /// <summary>The view of <see cref="Source"/>, or null for a direct source query.</summary>
    public string? View { get; init; }

    /// <summary>The result field the input takes.</summary>
    public required string Field { get; init; }

    /// <summary>The step's inputs with the same group are filled from the same result.</summary>
    public required string Group { get; init; }

    /// <summary>The result fields that identify a result to a person, in display order (may be empty).</summary>
    public IReadOnlyList<string> Display { get; init; } = [];
}

[ExcludeFromCodeCoverage]
public record ProcessorOutputFieldDescriptor : ProcessorPropertyDescriptor;

[ExcludeFromCodeCoverage]
public sealed record ProcessorStepResultDescriptor
{
    public IReadOnlyCollection<ProcessorOutputFieldDescriptor> OutputFields { get; init; }
        = [];
}

public interface IProcessorRegistry
{
    IReadOnlyCollection<ProcessorRegistryItem> Registrations { get; }
}

internal sealed partial class ProcessorRegistry : IProcessorRegistry
{

    public ProcessorRegistry(
        ITypeDescriber typeDescriber,
        IConstraintMapper constraintMapper,
        KaleidoServiceOptions serviceOptions,
        IProcessorStepRegistry stepRegistry,
        ILogger<ProcessorRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(typeDescriber);
        ArgumentNullException.ThrowIfNull(constraintMapper);
        ArgumentNullException.ThrowIfNull(serviceOptions);
        ArgumentNullException.ThrowIfNull(stepRegistry);
        ArgumentNullException.ThrowIfNull(logger);

        Registrations =
        [
            new ProcessorRegistryItem
            {
                IsEntryProcessor = serviceOptions.IsEntryProcessor,
                InitialSteps = stepRegistry.InitialRegistrations
                    .OrderBy(x => x.Metadata.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.ToSummary())
                    .ToArray(),
                Steps = stepRegistry.Registrations
                    .OrderBy(x => x.Metadata.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(x =>
                        x.ToRegistryItem(
                            typeDescriber,
                            constraintMapper))
                    .ToArray(),
            },
        ];

        LogRegistryBuilt(logger, serviceOptions.ServiceName, stepRegistry.Registrations.Count, stepRegistry.InitialRegistrations.Count);
    }

    public IReadOnlyCollection<ProcessorRegistryItem> Registrations { get; }

    [LoggerMessage(Level = LogLevel.Information, Message = "Process registry built for processor {ServiceName} with {StepCount} steps ({InitialCount} initial).")]
    private static partial void LogRegistryBuilt(ILogger logger, string serviceName, int stepCount, int initialCount);
}
