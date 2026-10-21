using Kaleido.Registry;

namespace Kaleido.Processor.Registry;

/// <summary>
/// A step registered with the processor: its step type, handler, relationships, repeatability and
/// published metadata. Built once at startup by step discovery; the processor's step registry holds
/// one per discovered step.
/// </summary>
/// <remarks>
/// A registration is published as a <see cref="ProcessorStepRegistryItem"/> in the processor registry
/// and as a <see cref="ProcessorStepSummary"/> (<see cref="ToSummary"/>) wherever another step names
/// it. <see cref="HandlerType"/>, <see cref="GetResultFromTask"/> and <see cref="InvokeHandlerAsync"/>
/// are runtime plumbing used to resolve and invoke the handler.
/// </remarks>
/// <param name="StepType">The step's type: the <see cref="IProcessStep"/> type that is the step's input.</param>
/// <param name="StepResultType">
/// The handler's typed response, the second type argument of
/// <see cref="IProcessStepHandler{TProcessStep, TProcessStepResult}"/>; <see langword="null"/> when the
/// handler implements <see cref="IProcessStepHandler{TProcessStep}"/> and returns no typed response.
/// </param>
/// <param name="HandlerType">The step's handler type, resolved from a dependency injection scope for each invocation.</param>
/// <param name="Dependencies">The registrations of the steps this step depends on (<c>[DependsOn]</c>).</param>
/// <param name="AvailableAfter">The registrations of the steps after which this step becomes available (<c>[AvailableAfter]</c>).</param>
/// <param name="AvailableUntil">The registrations of the steps that close this step (<c>[AvailableUntil]</c>).</param>
/// <param name="Repeatable">Whether the step may run again after it completed (<see cref="RepeatableAttribute"/>).</param>
/// <param name="Metadata">The step's published metadata, from its <see cref="ProcessStepAttribute"/>.</param>
/// <param name="GetResultFromTask">
/// Runtime plumbing: reads the <see cref="IProcessStepHandlerResult"/> from the completed task
/// returned by the handler's <c>ExecuteAsync</c>. Set by discovery; the runtime fails with a
/// framework error when it is <see langword="null"/>.
/// </param>
/// <param name="InvokeHandlerAsync">
/// Runtime plumbing: a compiled delegate that calls the handler's <c>ExecuteAsync</c> with the handler
/// instance, the step input, the <see cref="ProcessStepContext"/> and a cancellation token. Set by
/// discovery; the runtime fails with a framework error when it is <see langword="null"/>.
/// </param>
internal sealed partial record ProcessStepRegistration(
    Type StepType,
    Type? StepResultType,
    Type HandlerType,
    IReadOnlyCollection<ProcessStepRegistration> Dependencies,
    IReadOnlyCollection<ProcessStepRegistration> AvailableAfter,
    IReadOnlyCollection<ProcessStepRegistration> AvailableUntil,
    RepeatableOptions Repeatable,
    ProcessStepMetadata Metadata,
    Func<Task, IProcessStepHandlerResult>? GetResultFromTask = null,
    Func<object, object, ProcessStepContext, CancellationToken, Task>? InvokeHandlerAsync = null)
{
    /// <summary>
    /// <see langword="true"/> when the step's input is the answers to an
    /// <see cref="InformationRequest"/> (it implements <see cref="IInformationStep"/>).
    /// </summary>
    public bool IsInformationStep =>
        typeof(IInformationStep).IsAssignableFrom(StepType);
}

/// <summary>A step's repeatability, from <see cref="RepeatableAttribute"/>.</summary>
[ExcludeFromCodeCoverage]
internal sealed record RepeatableOptions
{
    /// <summary>
    /// <see langword="true"/> when the step type declares <see cref="RepeatableAttribute"/>, so the step
    /// may run again after it completed.
    /// </summary>
    public bool Enabled { get; init; }
}

/// <summary>A step's published metadata, from its <see cref="ProcessStepAttribute"/>.</summary>
[ExcludeFromCodeCoverage]
internal sealed record ProcessStepMetadata
{
    /// <summary>The step's name: its type name.</summary>
    public required string Name { get; init; }

    /// <inheritdoc cref="ProcessStepAttribute.Description"/>
    public required string Description { get; init; }

    /// <inheritdoc cref="ProcessStepAttribute.Version"/>
    public required string Version { get; init; }

    /// <inheritdoc cref="ProcessStepAttribute.DisplayName"/>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The effective authorization rule. <see cref="AuthorizationMetadata.Unspecified"/> means
    /// neither the step nor its service declared a rule.
    /// </summary>
    public AuthorizationMetadata Authorization { get; init; } = AuthorizationMetadata.Unspecified;

    /// <inheritdoc cref="ProcessStepAttribute.Destructive"/>
    public bool Destructive { get; init; }

    /// <inheritdoc cref="ProcessStepAttribute.External"/>
    public bool External { get; init; }

    /// <inheritdoc cref="ProcessStepAttribute.RequiresConfirmation"/>
    public bool RequiresConfirmation { get; init; }
}
