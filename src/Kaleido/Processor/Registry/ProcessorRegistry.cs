using Kaleido.Registry;
using Microsoft.Extensions.Logging;

namespace Kaleido.Processor.Registry;

/// <summary>
/// The processor as published in its registry: whether it is the entry processor, the steps that can
/// start a process and every registered step. Built once at startup from the discovered steps.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ProcessorRegistryItem
{
    /// <summary>
    /// Marks this processor as the entry point for the application workflow.
    /// When true, consumers should start with this processor.
    /// Only one processor in a distributed system should have this set to true.
    /// </summary>
    public bool IsEntryProcessor { get; init; }

    /// <summary>
    /// The steps that can start a new process: registered steps with no <c>[DependsOn]</c> or
    /// <c>[AvailableAfter]</c> relationship, excluding information steps. Ordered by name
    /// (case-insensitive).
    /// </summary>
    public IReadOnlyCollection<ProcessorStepSummary> InitialSteps { get; init; }
        = [];

    /// <summary>Every step the processor registers, ordered by name (case-insensitive).</summary>
    public IReadOnlyCollection<ProcessorStepRegistryItem> Steps { get; init; }
        = [];
}

/// <summary>
/// What every published description of a step carries: the full registry item and the summaries
/// that name a step elsewhere (initial steps, relationships, next and required steps).
/// </summary>
[ExcludeFromCodeCoverage]
internal abstract record ProcessorStepDescriptor
{
    /// <summary>The step's name: its type name.</summary>
    public required string Name { get; init; }

    /// <inheritdoc cref="ProcessStepAttribute.Description"/>
    public string? Description { get; init; }

    /// <inheritdoc cref="ProcessStepAttribute.DisplayName"/>
    public string? DisplayName { get; init; }

    /// <inheritdoc cref="ProcessStepAttribute.Version"/>
    public string? Version { get; init; }

    /// <summary><see langword="true"/> when the step may run again after it completed (<see cref="RepeatableAttribute"/>).</summary>
    public bool Repeatable { get; init; }

    /// <summary>
    /// <see langword="true"/> when the step's input is the answers to an information request; its
    /// fields are then empty and the pending request describes what to answer.
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

/// <summary>A step as published in the processor registry: its description, inputs, relationships and result.</summary>
[ExcludeFromCodeCoverage]
internal sealed record ProcessorStepRegistryItem : ProcessorStepDescriptor
{
    /// <summary>The step's input fields; empty for an information step.</summary>
    public IReadOnlyCollection<ProcessorInputFieldDescriptor> Fields { get; init; }
        = [];

    /// <summary>The steps this step depends on (<c>[DependsOn]</c>).</summary>
    public IReadOnlyCollection<ProcessorStepSummary> Dependencies { get; init; }
        = [];

    /// <summary>The steps after which this step becomes available (<c>[AvailableAfter]</c>).</summary>
    public IReadOnlyCollection<ProcessorStepSummary> AvailableAfter { get; init; }
        = [];

    /// <summary>The steps that close this step (<c>[AvailableUntil]</c>).</summary>
    public IReadOnlyCollection<ProcessorStepSummary> AvailableUntil { get; init; }
        = [];

    /// <summary>The step's result fields, when its handler returns a typed response.</summary>
    public ProcessorStepResultDescriptor? Result { get; init; }
}

/// <summary>A step named from elsewhere: an initial step, a relationship, a next or the required step.</summary>
[ExcludeFromCodeCoverage]
internal sealed record ProcessorStepSummary : ProcessorStepDescriptor;

/// <summary>
/// A published property of a step: an input field of the step or an output field of its result.
/// </summary>
[ExcludeFromCodeCoverage]
internal record ProcessorPropertyDescriptor
{
    /// <summary>The property's name, as declared on its type.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The property's description, from <c>[Description]</c>; for an input field, from
    /// <c>[Display(Description = …)]</c> when it has no <c>[Description]</c>. Null when neither is declared.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>The property's data type, as described for the published contracts.</summary>
    public required DataTypeDescriptor DataType { get; init; }

    /// <summary>
    /// The field's data classifications (<c>DataClassificationAttribute</c>, e.g. <c>"PriorAuth:PHI"</c>),
    /// so clients know which values are sensitive. Empty when the field is not classified.
    /// </summary>
    public IReadOnlyList<string> Classifications { get; init; } = [];
}

/// <summary>
/// An input field of a step: a public property of the step type, with its display hints, lookup and
/// validation constraints.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ProcessorInputFieldDescriptor : ProcessorPropertyDescriptor
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

    /// <summary>
    /// The input's validation constraints, one per <c>ValidationAttribute</c> (DataAnnotations) on the
    /// property. Empty when it declares none.
    /// </summary>
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
internal sealed record LookupMetadata
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

/// <summary>An output field of a step's result: a public readable instance property of the handler's typed response.</summary>
[ExcludeFromCodeCoverage]
internal sealed record ProcessorOutputFieldDescriptor : ProcessorPropertyDescriptor;

/// <summary>
/// The result of a step whose handler returns a typed response
/// (<see cref="Kaleido.Processor.Execution.IProcessStepHandler{TProcessStep, TProcessStepResult}"/>).
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ProcessorStepResultDescriptor
{
    /// <summary>
    /// The response type's public readable instance properties. Empty when the response type is a
    /// string, a primitive or an enum.
    /// </summary>
    public IReadOnlyCollection<ProcessorOutputFieldDescriptor> OutputFields { get; init; }
        = [];
}

internal interface IProcessorRegistry
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
