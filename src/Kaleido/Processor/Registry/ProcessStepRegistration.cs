using Kaleido.Registry;

namespace Kaleido.Processor.Registry;

[ExcludeFromCodeCoverage]
public sealed partial record ProcessStepRegistration(
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

[ExcludeFromCodeCoverage]
public sealed record RepeatableOptions
{
    public bool Enabled { get; init; }
}

/// <summary>A step's published metadata, from its <see cref="ProcessStepAttribute"/>.</summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepMetadata
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
